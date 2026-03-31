using System;
using UnityEngine;
using UnityEngine.AI;

namespace AutoBattler
{
    public sealed class BattleUnit : MonoBehaviour
    {
        public static event Action<BattleUnit, BattleUnit> UnitDied;

        private UnitDefinition unitDefinition;
        private int[] ammunitionCounts;
        private int currentHealth;
        private float nextAttackTime;
        private float nextMoveReliabilityCheckTime;
        private float movementBreakdownEndTime;
        private float nextObjectiveRefreshTime;
        private Vector3 lastRequestedDestination;
        private Vector3 objectiveApproachOffset;
        private bool hasRequestedDestination;
        private Vector3 guardPosition;
        private Vector3 objectivePosition;
        private float groundOffset;
        private bool isAlive;
        private NavMeshAgent navigationAgent;

        public Team Team { get; private set; }
        public MissionType Mission { get; private set; }
        public UnitDefinition Definition => unitDefinition;
        public bool IsAlive => isAlive;
        public int CurrentHealth => currentHealth;
        public float RemainingReloadTime => Mathf.Max(0f, nextAttackTime - Time.time);
        public float CurrentMoveSpeed => GetEffectiveMoveSpeed();
        public float CurrentVelocity => navigationAgent != null ? navigationAgent.velocity.magnitude : 0f;
        public bool IsMovementTemporarilyBlocked => IsMovementBroken();
        public string NavigationAgentTypeName => navigationAgent != null ? NavMesh.GetSettingsNameFromID(navigationAgent.agentTypeID) : string.Empty;
        public int NavigationAgentTypeId => navigationAgent != null ? navigationAgent.agentTypeID : NavMeshAgentTypeResolver.GetDefaultAgentTypeId();
        public string NavigationPathStatus => navigationAgent != null && navigationAgent.hasPath ? navigationAgent.pathStatus.ToString() : "NoPath";
        public string OwnedUnitCardId { get; private set; }
        public string LootTableId { get; private set; }

        public void Initialize(UnitDefinition definition, Team team, MissionType mission, Vector3 homePosition, Vector3 objective, string lootTableId = null)
        {
            unitDefinition = definition;
            Team = team;
            Mission = mission;
            LootTableId = lootTableId ?? string.Empty;
            currentHealth = definition.MaxHealth;
            guardPosition = homePosition;
            objectivePosition = objective;
            groundOffset = GetGroundOffset();
            ammunitionCounts = CreateAmmoPool(definition);
            isAlive = true;
            nextAttackTime = 0f;
            nextMoveReliabilityCheckTime = Time.time + CombatRoller.GetMovementReliabilityCheckInterval(definition.MoveReliability);
            movementBreakdownEndTime = 0f;
            nextObjectiveRefreshTime = 0f;
            lastRequestedDestination = Vector3.zero;
            objectiveApproachOffset = BuildObjectiveApproachOffset(homePosition, team);
            hasRequestedDestination = false;
            navigationAgent = GetOrCreateNavigationAgent();

            SnapToGround(homePosition);
            ReapplyNavigationCosts();
            BattleUnitRegistry.Register(this);
        }

        private void Update()
        {
            if (!isAlive || unitDefinition == null)
            {
                return;
            }

            if (BattleStateManager.Instance != null && BattleStateManager.Instance.IsBattleOver)
            {
                ClearMovement();
                return;
            }

            var visibleEnemy = BattleUnitRegistry.GetClosestEnemy(this, unitDefinition.VisionRange);

            if (visibleEnemy != null)
            {
                Engage(visibleEnemy);
                return;
            }

            if (Mission == MissionType.Guard)
            {
                ReturnToGuardPosition();
                return;
            }

            MoveTowards(ResolveObjectivePosition());
        }

        public void ApplyDamage(int incomingDamage, BattleUnit attacker)
        {
            if (!isAlive)
            {
                return;
            }

            var resolvedDamage = DamageResolver.Resolve(incomingDamage, unitDefinition.Armor);
            if (resolvedDamage <= 0)
            {
                return;
            }

            currentHealth -= resolvedDamage;
            if (currentHealth <= 0)
            {
                Die(attacker);
            }
        }

        public void LinkOwnedUnitCard(string ownedUnitCardId)
        {
            OwnedUnitCardId = ownedUnitCardId;
        }

        private void Engage(BattleUnit target)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            FaceTowards(target.transform.position);

            var distance = GetDistanceTo(target.transform.position);
            var maxAttackRange = GetMaxAvailableAttackRange();
            if (maxAttackRange <= 0f)
            {
                ClearMovement();
                return;
            }

            if (distance > maxAttackRange)
            {
                MoveTowards(target.transform.position, Mathf.Max(0.25f, maxAttackRange * 0.9f));
                return;
            }

            PauseMovement();

            if (Time.time < nextAttackTime)
            {
                return;
            }

            if (!TrySelectBestAmmo(target, distance, out var ammoIndex, out var ammo))
            {
                return;
            }

            nextAttackTime = Time.time + ammo.ReloadTime;
            ConsumeAmmo(ammoIndex);

            if (!CombatRoller.RollProbability(unitDefinition.FireReliability))
            {
                return;
            }

            var finalAccuracy = CombatRoller.CombineProbability(unitDefinition.Accuracy, ammo.Accuracy);
            var impactPoint = CombatRoller.ResolveImpactPoint(target.transform.position, distance, finalAccuracy);
            if (!CombatRoller.RollProbability(ammo.DamageReliability))
            {
                return;
            }

            BattleUnitRegistry.ApplySplashDamage(Team, impactPoint, ResolveOutgoingDamage(ammo), ammo.Radius, this);
        }

        private bool TrySelectBestAmmo(BattleUnit target, float distanceToTarget, out int bestAmmoIndex, out AmmoDefinition bestAmmo)
        {
            bestAmmo = null;
            bestAmmoIndex = -1;
            var bestScore = int.MinValue;
            var ammunition = unitDefinition.Ammunition;

            if (ammunition == null)
            {
                return false;
            }

            for (var i = 0; i < ammunition.Length; i++)
            {
                var candidate = ammunition[i];
                if (candidate == null
                    || candidate.RequiredUserType != unitDefinition.UnitType
                    || candidate.AttackRange < distanceToTarget
                    || !HasAmmoRemaining(i))
                {
                    continue;
                }

                var splashHits = BattleUnitRegistry.CountEnemiesInRadius(Team, target.transform.position, candidate.Radius);
                var score = splashHits * candidate.Damage;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAmmo = candidate;
                    bestAmmoIndex = i;
                }
            }

            return bestAmmo != null;
        }

        private float GetMaxAvailableAttackRange()
        {
            var ammunition = unitDefinition.Ammunition;
            if (ammunition == null)
            {
                return 0f;
            }

            var maxRange = 0f;
            for (var i = 0; i < ammunition.Length; i++)
            {
                var ammo = ammunition[i];
                if (ammo == null || ammo.RequiredUserType != unitDefinition.UnitType || !HasAmmoRemaining(i))
                {
                    continue;
                }

                maxRange = Mathf.Max(maxRange, ammo.AttackRange);
            }

            return maxRange;
        }

        private float GetMaxConfiguredAttackRange()
        {
            var ammunition = unitDefinition.Ammunition;
            if (ammunition == null)
            {
                return 0f;
            }

            var maxRange = 0f;
            for (var i = 0; i < ammunition.Length; i++)
            {
                var ammo = ammunition[i];
                if (ammo == null || ammo.RequiredUserType != unitDefinition.UnitType)
                {
                    continue;
                }

                maxRange = Mathf.Max(maxRange, ammo.AttackRange);
            }

            return maxRange;
        }

        private void MoveTowards(Vector3 targetPosition, float stoppingDistance = 0f)
        {
            if (IsMovementBroken())
            {
                ClearMovement();
                return;
            }

            if (ShouldTriggerMovementBreakdown())
            {
                ClearMovement();
                return;
            }

            var effectiveSpeed = GetEffectiveMoveSpeed();
            if (CanUseNavigation())
            {
                var navigationDestination = GetNavigationPosition(targetPosition);
                navigationAgent.speed = effectiveSpeed;
                navigationAgent.acceleration = Mathf.Max(8f, effectiveSpeed * 4f);
                var requestedDestination = CalculateNavigationDestination(navigationDestination, stoppingDistance);
                if (!TryResolveNavigableDestination(requestedDestination, out var navigableDestination))
                {
                    PauseMovement();
                    return;
                }

                navigationAgent.stoppingDistance = 0.1f;
                navigationAgent.isStopped = false;
                TryUpdateDestination(navigableDestination);
                return;
            }

            var destination = GetGroundedPosition(targetPosition);
            var flatDestination = new Vector3(destination.x, transform.position.y, destination.z);
            var nextPosition = Vector3.MoveTowards(
                transform.position,
                flatDestination,
                effectiveSpeed * Time.deltaTime);

            transform.position = GetGroundedPosition(nextPosition);
            FaceTowards(flatDestination);
        }

        private bool IsMovementBroken()
        {
            return Time.time < movementBreakdownEndTime;
        }

        private bool ShouldTriggerMovementBreakdown()
        {
            if (unitDefinition.MoveReliability >= 0.999f)
            {
                return false;
            }

            if (Time.time < nextMoveReliabilityCheckTime)
            {
                return false;
            }

            nextMoveReliabilityCheckTime = Time.time + CombatRoller.GetMovementReliabilityCheckInterval(unitDefinition.MoveReliability);
            if (CombatRoller.RollProbability(unitDefinition.MoveReliability))
            {
                return false;
            }

            movementBreakdownEndTime = Time.time + CombatRoller.GetMovementBreakdownDuration(unitDefinition.MoveReliability);
            return true;
        }

        private void ReturnToGuardPosition()
        {
            if (GetDistanceTo(guardPosition) > 0.1f)
            {
                MoveTowards(guardPosition, 0.1f);
                return;
            }

            ClearMovement();
        }

        private void FaceTowards(Vector3 targetPosition)
        {
            var direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private float GetDistanceTo(Vector3 targetPosition)
        {
            var delta = targetPosition - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void Die(BattleUnit attacker)
        {
            if (!isAlive)
            {
                return;
            }

            isAlive = false;
            ClearMovement();
            BattleUnitRegistry.Unregister(this);

            if (attacker != null && ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddPoint(attacker.Team);
            }

            UnitDied?.Invoke(this, attacker);
            Destroy(gameObject);
        }

        private void SnapToGround(Vector3 preferredPosition)
        {
            var navigationPosition = GetNavigationPosition(preferredPosition);
            if (TryWarpToNavigation(navigationPosition))
            {
                return;
            }

            transform.position = GetGroundedPosition(preferredPosition);
        }

        private Vector3 GetGroundedPosition(Vector3 position)
        {
            var groundedPosition = GetNavigationPosition(position);
            groundedPosition.y += groundOffset;
            return groundedPosition;
        }

        private Vector3 GetNavigationPosition(Vector3 position)
        {
            var groundedPosition = position;
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                groundedPosition.y = 0f;
                return groundedPosition;
            }

            var terrainOrigin = terrain.GetPosition();
            var localX = groundedPosition.x - terrainOrigin.x;
            var localZ = groundedPosition.z - terrainOrigin.z;
            var terrainSize = terrain.terrainData.size;

            if (localX >= 0f && localX <= terrainSize.x && localZ >= 0f && localZ <= terrainSize.z)
            {
                groundedPosition.y = terrain.SampleHeight(groundedPosition) + terrainOrigin.y;
                return groundedPosition;
            }

            groundedPosition.y = 0f;
            return groundedPosition;
        }

        private float GetGroundOffset()
        {
            var ownCollider = GetComponentInChildren<Collider>();
            return ownCollider != null ? ownCollider.bounds.extents.y : 0.5f;
        }

        private NavMeshAgent GetOrCreateNavigationAgent()
        {
            var agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = gameObject.AddComponent<NavMeshAgent>();
            }

            agent.agentTypeID = NavMeshAgentTypeResolver.ResolveAgentTypeId(unitDefinition.NavigationAgentType);
            agent.speed = unitDefinition.Speed;
            agent.acceleration = Mathf.Max(8f, unitDefinition.Speed * 4f);
            agent.angularSpeed = 720f;
            agent.autoBraking = false;
            agent.updateRotation = true;
            agent.obstacleAvoidanceType = unitDefinition.UnitType == UnitType.Infantry
                ? ObstacleAvoidanceType.LowQualityObstacleAvoidance
                : ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agent.avoidancePriority = Mathf.Clamp(50 + GetAvoidancePriorityOffset(guardPosition, Team), 20, 80);
            agent.baseOffset = groundOffset;
            agent.radius = GetNavigationRadius();
            agent.height = Mathf.Max(1f, groundOffset * 2f);
            ReapplyNavigationCosts(agent);

            return agent;
        }

        private float GetEffectiveMoveSpeed()
        {
            if (BattleNavigationManager.Instance == null)
            {
                return unitDefinition.Speed;
            }

            return Mathf.Max(0.1f, unitDefinition.Speed * BattleNavigationManager.Instance.GetSpeedMultiplier(unitDefinition, transform.position));
        }

        private float GetNavigationRadius()
        {
            var ownCollider = GetComponentInChildren<Collider>();
            if (ownCollider == null)
            {
                return unitDefinition.UnitType == UnitType.Tank ? 0.8f : 0.45f;
            }

            var extents = ownCollider.bounds.extents;
            return Mathf.Max(0.35f, Mathf.Min(Mathf.Max(extents.x, extents.z) * 0.7f, 1.2f));
        }

        private bool TryWarpToNavigation(Vector3 preferredPosition)
        {
            if (navigationAgent == null)
            {
                return false;
            }

            if (!TryResolveNavigableDestination(preferredPosition, out var hitPosition))
            {
                return false;
            }

            return navigationAgent.Warp(hitPosition);
        }

        private void ReapplyNavigationCosts()
        {
            ReapplyNavigationCosts(navigationAgent);
        }

        private void ReapplyNavigationCosts(NavMeshAgent agent)
        {
            if (agent == null || BattleNavigationManager.Instance == null)
            {
                return;
            }

            BattleNavigationManager.Instance.ConfigureAgent(agent, unitDefinition);
        }

        private bool CanUseNavigation()
        {
            return navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh;
        }

        private void PauseMovement()
        {
            if (!CanUseNavigation())
            {
                return;
            }

            navigationAgent.isStopped = true;
        }

        private void ClearMovement()
        {
            if (!CanUseNavigation())
            {
                return;
            }

            navigationAgent.isStopped = true;
            navigationAgent.ResetPath();
            hasRequestedDestination = false;
        }

        private Vector3 CalculateNavigationDestination(Vector3 rawDestination, float stoppingDistance)
        {
            if (stoppingDistance <= 0.1f)
            {
                return rawDestination;
            }

            var offset = rawDestination - transform.position;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (distance <= stoppingDistance)
            {
                return transform.position;
            }

            return rawDestination - (offset.normalized * stoppingDistance);
        }

        private void TryUpdateDestination(Vector3 destination)
        {
            if (!CanUseNavigation())
            {
                return;
            }

            var destinationChanged = !hasRequestedDestination || Vector3.SqrMagnitude(lastRequestedDestination - destination) >= 0.25f;
            var needsRecovery = !navigationAgent.hasPath || navigationAgent.pathStatus != NavMeshPathStatus.PathComplete;
            if (!destinationChanged && !needsRecovery)
            {
                return;
            }

            navigationAgent.SetDestination(destination);
            lastRequestedDestination = destination;
            hasRequestedDestination = true;
        }

        private bool TryResolveNavigableDestination(Vector3 preferredPosition, out Vector3 navigablePosition)
        {
            navigablePosition = preferredPosition;
            if (navigationAgent == null)
            {
                return false;
            }

            var maxDistance = Mathf.Max(4f, navigationAgent.radius * 6f);
            var areaMask = NavMesh.AllAreas;
            if (NavMesh.SamplePosition(preferredPosition, out var hit, maxDistance, areaMask))
            {
                navigablePosition = hit.position;
                return true;
            }

            return false;
        }

        private static int[] CreateAmmoPool(UnitDefinition definition)
        {
            var counts = definition.AmmunitionCounts;
            if (counts == null)
            {
                return Array.Empty<int>();
            }

            var pool = new int[counts.Length];
            for (var i = 0; i < counts.Length; i++)
            {
                pool[i] = counts[i];
            }

            return pool;
        }

        private bool HasAmmoRemaining(int ammoIndex)
        {
            if (ammunitionCounts == null || ammoIndex < 0 || ammoIndex >= ammunitionCounts.Length)
            {
                return false;
            }

            return ammunitionCounts[ammoIndex] < 0 || ammunitionCounts[ammoIndex] > 0;
        }

        private void ConsumeAmmo(int ammoIndex)
        {
            if (ammunitionCounts == null || ammoIndex < 0 || ammoIndex >= ammunitionCounts.Length)
            {
                return;
            }

            if (ammunitionCounts[ammoIndex] < 0)
            {
                return;
            }

            ammunitionCounts[ammoIndex] = Mathf.Max(0, ammunitionCounts[ammoIndex] - 1);
        }

        public int GetAmmoRemaining(int ammoIndex)
        {
            if (ammunitionCounts == null || ammoIndex < 0 || ammoIndex >= ammunitionCounts.Length)
            {
                return 0;
            }

            return ammunitionCounts[ammoIndex];
        }

        private int ResolveOutgoingDamage(AmmoDefinition ammo)
        {
            var damage = ammo != null ? ammo.Damage : 0;
            var bonusMin = unitDefinition != null ? unitDefinition.OutgoingDamageBonusMin : 0;
            var bonusMax = unitDefinition != null ? unitDefinition.OutgoingDamageBonusMax : 0;
            if (bonusMax <= 0)
            {
                return damage;
            }

            return Mathf.Max(0, damage + CombatRoller.RollInclusive(Mathf.Max(0, bonusMin), Mathf.Max(0, bonusMax)));
        }

        public float GetAreaCost(int areaIndex)
        {
            if (navigationAgent == null)
            {
                return 1f;
            }

            return navigationAgent.GetAreaCost(areaIndex);
        }

        public bool TryCalculatePathTo(Vector3 destination, out NavMeshPathStatus pathStatus, out float pathLength)
        {
            pathStatus = NavMeshPathStatus.PathInvalid;
            pathLength = 0f;
            if (!CanUseNavigation())
            {
                return false;
            }

            var path = new NavMeshPath();
            if (!navigationAgent.CalculatePath(destination, path))
            {
                return false;
            }

            pathStatus = path.status;
            pathLength = CalculatePathLength(path);
            return true;
        }

        private void OnDestroy()
        {
            BattleUnitRegistry.Unregister(this);
        }

        private void OnDrawGizmosSelected()
        {
            if (unitDefinition == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, unitDefinition.VisionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, GetMaxConfiguredAttackRange());

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ResolveObjectivePosition(), 0.5f);
        }

        private Vector3 ResolveObjectivePosition()
        {
            if (BattleObjectiveManager.Instance == null)
            {
                return objectivePosition;
            }

            if (Time.time >= nextObjectiveRefreshTime || GetDistanceTo(objectivePosition) <= 2f)
            {
                objectivePosition = BattleObjectiveManager.Instance.GetObjectiveDestination(Team, transform.position, objectivePosition) + objectiveApproachOffset;
                nextObjectiveRefreshTime = Time.time + 0.75f;
            }

            return objectivePosition;
        }

        private Vector3 BuildObjectiveApproachOffset(Vector3 referencePosition, Team team)
        {
            var magnitude = unitDefinition != null && unitDefinition.UnitType == UnitType.Infantry ? 0.35f : 0.2f;
            var seed = Mathf.Abs((referencePosition.x * 0.173f) + (referencePosition.z * 0.619f) + ((int)team * 0.271f));
            var raw = seed - Mathf.Floor(seed);
            var angle = raw * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * magnitude;
        }

        private int GetAvoidancePriorityOffset(Vector3 referencePosition, Team team)
        {
            var seed = Mathf.Abs((referencePosition.x * 11.3f) + (referencePosition.z * 7.7f) + ((int)team * 3.1f));
            var raw = Mathf.FloorToInt((seed - Mathf.Floor(seed)) * 31f);
            return raw - 15;
        }

        private static float CalculatePathLength(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
            {
                return 0f;
            }

            var length = 0f;
            for (var i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            return length;
        }
    }
}
