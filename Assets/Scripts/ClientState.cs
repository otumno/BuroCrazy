public enum ClientState
{
    Spawning, MovingToGoal, MovingToSeat, MovingToRegistrarImpolite, // <-- ДОБАВЛЕНО НОВОЕ СОСТОЯНИЕ
    AtRegistration, AtWaitingArea, SittingInWaitingArea,
    AtToilet, Leaving, Confused,
    PassedRegistration, Positioning, ReturningToWait, AtDesk1, AtDesk2,
    AtLimitedZoneEntrance, InsideLimitedZone,
    
    Enraged
}