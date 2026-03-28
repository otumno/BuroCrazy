// Файл: ClientState.cs
public enum ClientState
{
    Spawning, MovingToTerminal, GettingTicket, InterruptingDesk, MovingToGoal, MovingToSeat, MovingToRegistrarImpolite,
    AtRegistration, AtWaitingArea, SittingInWaitingArea,
    AtToilet, Leaving, Confused,
    PassedRegistration,
    GoingToCashier, AtCashier,
    Positioning, ReturningToWait, AtDesk1, AtDesk2,
    AtLimitedZoneEntrance, InsideLimitedZone,
    Grumbling,    // Ворчание - промежуточное состояние недовольства
    Enraged,
    LeavingUpset,
    ReturningToRegistrar,
	WaitingForDocument
}