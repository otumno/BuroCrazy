// Файл: ActionType.cs - ОБНОВЛЕННАЯ ВЕРСИЯ

public enum ActionType
{
    // --- НОВЫЕ ДЕЙСТВИЯ КЛЕРКА ---
    ProcessDocumentCat1,    // Обработать документ 1 категории
    ProcessDocumentCat2,    // Обработать документ 2 категории
    CheckDocument,          // Проверить документ на ошибки
    HandleSituation,        // Разобраться с ситуацией (перенаправить клиента)
    ClientOrientedService,  // Поболтать с клиентом (снизить нетерпение)
    SortPapers,             // Разобрать бумаги на столе (снизить стресс)
    
    // --- ОБЩЕЕ ДЕЙСТВИЕ ДЛЯ КЛЕРКА, РЕГИСТРАТОРА, КАССИРА ---
    TakeStackToArchive,     // Отнести стопку документов в архив

    // --- НОВОЕ ДЕЙСТВИЕ РЕГИСТРАТОРА ---
    GiveConsultation,       // Дать быструю консультацию (для клиентов AskAndLeave)
	PrioritizeConsultation,   // Приоритет: Устная справка
	PrioritizeDirectorDoc,    // Приоритет: Документы Директора
	PrioritizePayment,        // Приоритет: Оплата
	ChairPatrol,
	MakeArchiveRequest,

    // --- СТАРЫЕ ДЕЙСТВИЯ (оставляем для других ролей) ---
    ArchiveDocument,        // Архивирование документа (для Архивариуса)
	CorrectDocument,
	RetrieveDocument,
	DoBookkeeping,
	PrepareSalaries,
	DirectorPrepareSalaries,

    // Действия Уборщика
    CleanTrash,             // Уборка мусора
    CleanPuddle,            // Уборка лужи
    CleanDirt,				// Уборка грязи
	FindValuablesInTrash,
	JanitorPatrol,
	EmptyTrashCan,	

    // Действия Стажера
    HelpConfusedClient,     // Помощь потерявшемуся клиенту
    ServeFromQueue,         // Обслуживание клиента из общей очереди
    DeliverDocuments,       // Доставка пачки документов в архив
    CoverDesk,              // Подмена сотрудника на рабочем месте
	CoverRegistrar,
	CoverClerk,
	CoverCashier,
	InternPatrol,

    // Действия Охранника
    PatrolWaypoint,         // Достижение одной точки патрулирования
    CatchThief,             // Успешная поимка воришки
    CalmDownViolator,       // Успешное усмирение нарушителя
    OperateBarrier, 
    EvictClient,
    None,

    // Старое действие ProcessDocument можно удалить или закомментировать,
    // так как мы заменили его на более конкретные ProcessDocumentCat1 и ProcessDocumentCat2
    // ProcessDocument,    
}