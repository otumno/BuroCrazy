// Файл: ClientState.cs
public enum ClientState
{
    Spawning, MovingToGoal, MovingToSeat, MovingToRegistrarImpolite,
    AtRegistration, AtWaitingArea, SittingInWaitingArea,
    AtToilet, Leaving, Confused,
    PassedRegistration,
    GoingToCashier, AtCashier,
    Positioning, ReturningToWait, AtDesk1, AtDesk2,
    AtLimitedZoneEntrance, InsideLimitedZone,
    Enraged,
    LeavingUpset,
    ReturningToRegistrar,
	WaitingForDocument	// <-- ДОБАВЬТЕ ЭТУ СТРОКУ
}