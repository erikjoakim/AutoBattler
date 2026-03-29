using System;
using UnityEngine;
using UnityEngine.AI;

namespace AutoBattler
{
    public sealed class BattleUnit : MonoBehaviour
    {
        private UnitDefinition unitDefinition;
        private int[] ammunitionCounts;
        private int currentHealth;
        private float nextAttackTime;
        private float nextMoveReliabilityCheckTime;
        private float movementBreakdownEndTime;
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

        public void Initialize(UnitDefinition definition, Team team, MissionType mission, Vector3 homePosition, Vector3 objective)
        {
            unitDefinition = definition;
            Team = team;
            Mission = mission;
            currentHealth = definition.MaxHealth;
            guardPosition = homePosition;
            objectivePosition = objective;
            groundOffset = GetGroundOffset();
            ammunitionCounts = CreateAmmoPool(definition);
            isAlive = true;
            nextAttackTime = 0f;
            nextMoveReliabilityCheckTime = Time.time + CombatRoller.GetMovementReliabilityCheckInterval(definition.MoveReliability);
            movementBreakdownEndTime = 0f;
            navigationAgent = GetOrCreateNavigationAgent();

            SnapToGround(homePosition);
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
                StopMovement();
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

            MoveTowards(objectivePosition);
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
                StopMovement();
                return;
            }

            if (distance > maxAttackRange)
            {
                MoveTowards(target.transform.position, Mathf.Max(0.25f, maxAttackRange * 0.9f));
                return;
            }

            StopMovement();

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

            BattleUnitRegistry.ApplySplashDamage(Team, impactPoint, ammo.Damage, ammo.Radius, this);
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
                StopMovement();
                return;
            }

            if (ShouldTriggerMovementBreakdown())
            {
                StopMovement();
                return;
            }

            var destination = GetGroundedPosition(targetPosition);
            var effectiveSpeed = GetEffectiveMoveSpeed();
            if (CanUseNavigation())
            {
                navigationAgent.speed = effectiveSpeed;
                navigationAgent.acceleration = Mathf.Max(8f, effectiveSpeed * 4f);
                navigationAgent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
                navigationAgent.isStopped = false;
                navigationAgent.SetDestination(destination);
                return;
            }

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

            StopMovement();
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
            StopMovement();
            BattleUnitRegistry.Unregister(this);

            if (attacker != null && ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddPoint(attacker.Team);
            }

            Destroy(gameObject);
        }

        private void SnapToGround(Vector3 preferredPosition)
        {
            var groundedPosition = GetGroundedPosition(preferredPosition);
            if (TryWarpToNavigation(groundedPosition))
            {
                return;
            }

            transform.position = groundedPosition;
        }

        private Vector3 GetGroundedPosition(Vector3 position)
        {
            var groundedPosition = position;
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                groundedPosition.y = groundOffset;
                return groundedPosition;
            }

            var terrainOrigin = terrain.GetPosition();
            var localX = groundedPosition.x - terrainOrigin.x;
            var localZ = groundedPosition.z - terrainOrigin.z;
            var terrainSize = terrain.terrainData.size;

            if (localX >= 0f && localX <= terrainSize.x && localZ >= 0f && localZ <= terrainSize.z)
            {
                groundedPosition.y = terrain.SampleHeight(groundedPosition) + terrainOrigin.y + groundOffset;
                return groundedPosition;
            }

            groundedPosition.y = groundOffset;
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
            agent.autoBraking = true;
            agent.updateRotation = true;
            agent.baseOffset = groundOffset;
            agent.radius = GetNavigationRadius();
            agent.height = Mathf.Max(1f, groundOffset * 2f);
            if (BattleNavigationManager.Instance != null)
            {
                BattleNavigationManager.Instance.ConfigureAgent(agent, unitDefinition);
            }

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

            if (!NavMesh.SamplePosition(preferredPosition, out var hit, 8f, NavMesh.AllAreas))
            {
                return false;
            }

            return navigationAgent.Warp(hit.position);
        }

        private bool CanUseNavigation()
        {
            return navigationAgent != null && navigationAgent.isActiveAndEnabled && navigationAgent.isOnNavMesh;
        }

        private void StopMovement()
        {
            if (!CanUseNavigation())
            {
                return;
            }

            navigationAgent.isStopped = true;
            navigationAgent.ResetPath();
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
            Gizmos.DrawWireSphere(objectivePosition, 0.5f);
        }
    }
}
