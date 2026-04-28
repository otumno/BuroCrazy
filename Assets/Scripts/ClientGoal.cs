// Файл: ClientGoal.cs
public enum ClientGoal
{
    GetCertificate1,
    GetCertificate2,
    PayTax,
    VisitToilet,
    AskAndLeave,
    DirectorApproval,
	DirectorAudience,
	GetArchiveRecord,	// Добавлена новая цель

	   // Подтипы для Certificate1
	   Certificate1A,
	   Certificate1B,
	   Certificate1C,

	   // Подтипы для Certificate2
	   Certificate2A,
	   Certificate2B,
	   Certificate2C,

	   // Подтипы для Form1 (бланки)
	   Form1A,
	   Form1B,
	   Form1C,

	   // Подтипы для Form2 (бланки)
	   Form2A,
	   Form2B,
	   Form2C
}