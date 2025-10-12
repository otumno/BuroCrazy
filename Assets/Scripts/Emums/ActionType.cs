// Файл: ActionType.cs - ОБНОВЛЕННАЯ ВЕРСИЯ

public enum ActionType
{
    // --- НОВЫЕ ДЕЙСТВИЯ ОБСЛУЖИВАНИЯ ---
    ServiceAtRegistration,    // Обслуживание в регистратуре
    ServiceAtCashier,         // Обслуживание в кассе
    ServiceAtOfficeDesk,      // Обслуживание за офисным столом

    // --- СТАРЫЕ ДЕЙСТВИЯ КЛЕРКА (некоторые теперь не нужны) ---
    ProcessDocumentCat1,
    ProcessDocumentCat2,
    CheckDocument,
    HandleSituation,
    ClientOrientedService,
    SortPapers,
    TakeStackToArchive,
    
    // --- ДЕЙСТВИЯ РЕГИСТРАТОРА ---
    GiveConsultation,
	PrioritizeConsultation,
	PrioritizeDirectorDoc,
	PrioritizePayment,
	ChairPatrol,
	MakeArchiveRequest,
    
    // --- ДЕЙСТВИЯ АРХИВАРИУСА И БУХГАЛТЕРА ---
    ArchiveDocument,
	CorrectDocument,
	RetrieveDocument,
	DoBookkeeping,
	PrepareSalaries,
	DirectorPrepareSalaries,

    // Действия Уборщика
    CleanTrash,
    CleanPuddle,
    CleanDirt,
	FindValuablesInTrash,
	JanitorPatrol,
	EmptyTrashCan,	

    // Действия Стажера
    HelpConfusedClient,
    ServeFromQueue,     
    DeliverDocuments,
    CoverDesk,
	CoverRegistrar,
	CoverClerk,
	CoverCashier,
	InternPatrol,

    // Действия Охранника
    PatrolWaypoint,
    CatchThief,
    CalmDownViolator,
    OperateBarrier, 
    EvictClient,
    None,
}