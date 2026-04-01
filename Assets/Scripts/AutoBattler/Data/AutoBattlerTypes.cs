namespace AutoBattler
{
    public enum Team
    {
        Blue,
        Red
    }

    public enum UnitType
    {
        Tank,
        Infantry
    }

    public enum MissionType
    {
        Guard,
        SeekAndDestroy
    }

    public enum PlayerMissionAssignmentType
    {
        UseUnitDefault,
        SeekObjective,
        AttackInfantryFirst,
        AttackTanksFirst,
        EscortAssignedTank,
        ScoutDoNotEngage
    }

    public enum MovementInstructionType
    {
        UseUnitDefault,
        HoldPosition,
        SeekVictoryPoint,
        FollowAssignedTank
    }

    public enum EngagementInstructionType
    {
        UseUnitDefault,
        AttackEnemies,
        AvoidEngagement
    }

    public enum PriorityInstructionType
    {
        UseUnitDefault,
        PrioritizeInfantry,
        PrioritizeTanks
    }

    public enum ObjectiveOwner
    {
        Neutral,
        Blue,
        Red
    }
}
