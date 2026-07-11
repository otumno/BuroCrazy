// Assets/Scripts/Utilities/NameGenerator.cs
using System.Collections.Generic;
using Enums;

namespace Utilities
{
    /// <summary>
    /// Статический генератор вымышленных имён в стиле «отражений» (по мотивам Чайны Мьевиля).
    /// Содержит списки имён, фамилий и патронимов для сотрудников и клиентов.
    /// EN-локализация закомментирована и готова к будущему включению.
    /// </summary>
    public static class NameGenerator
    {
        /// <summary>
        /// Тройка имени: полная форма / сокращение / уменьшительная.
        /// </summary>
        public readonly struct NameSet
        {
            public readonly string full;
            public readonly string shortName;
            public readonly string diminutive;

            public NameSet(string full, string shortName, string diminutive)
            {
                this.full = full;
                this.shortName = shortName;
                this.diminutive = diminutive;
            }
        }

        // =====================================================================
        // МУЖСКИЕ ИМЕНА (40 шт.)
        // Все имена вымышленные и не существуют в реальных языках.
        // =====================================================================
        private static readonly NameSet[] maleNames = new NameSet[]
        {
            new NameSet("Тьядор",   "Тья",   "Тьян"),
            new NameSet("Харрье",   "Харр",  "Харри"),
            new NameSet("Киентин",  "Киен",  "Кини"),
            new NameSet("Паксель",  "Пакс",  "Пакси"),
            new NameSet("Лотарье",  "Лот",   "Лоти"),
            new NameSet("Геретн",   "Гере",  "Гери"),
            new NameSet("Эврат",    "Эвр",   "Эври"),
            new NameSet("Корван",   "Кор",   "Кори"),
            new NameSet("Элиант",   "Эли",   "Эли"),
            new NameSet("Ноласс",   "Нол",   "Ноли"),
            new NameSet("Дариен",   "Дар",   "Дари"),
            new NameSet("Фавьен",   "Фав",   "Фави"),
            new NameSet("Геркгор",  "Герк",  "Герки"),
            new NameSet("Изадер",   "Иза",   "Изи"),
            new NameSet("Улиант",   "Ули",   "Ули"),
            new NameSet("Рауланд",  "Раул",  "Раули"),
            new NameSet("Морианс",  "Мори",  "Мори"),
            new NameSet("Кэллар",   "Кэлл",  "Кэлли"),
            new NameSet("Орриан",   "Орр",   "Орри"),
            new NameSet("Сайлесс",  "Сай",   "Сайли"),
            new NameSet("Бальтар",  "Бал",   "Балли"),
            new NameSet("Верик",    "Вер",   "Вери"),
            new NameSet("Гаррен",   "Гар",   "Гарри"),
            new NameSet("Дерель",   "Дер",   "Дери"),
            new NameSet("Жервель",  "Жер",   "Жери"),
            new NameSet("Зигвар",   "Зиг",   "Зиги"),
            new NameSet("Ильмар",   "Иль",   "Ильми"),
            new NameSet("Клавиан",  "Клав",  "Клави"),
            new NameSet("Ларвин",   "Лар",   "Лари"),
            new NameSet("Мельхиор", "Мель",  "Мелли"),
            new NameSet("Нервин",   "Нер",   "Нери"),
            new NameSet("Олафред",  "Ола",   "Оли"),
            new NameSet("Периваль", "Пер",   "Пери"),
            new NameSet("Рейнарт",  "Рей",   "Рейни"),
            new NameSet("Сильвестер","Силь", "Силли"),
            new NameSet("Торвальд", "Тор",   "Тори"),
            new NameSet("Ульфик",   "Уль",   "Ульфи"),
            new NameSet("Фергюс",   "Фер",   "Ферри"),
            new NameSet("Хардиан",  "Хард",  "Харди"),
            new NameSet("Эдварт",   "Эд",    "Эдди"),
        };

        // =====================================================================
        // ЖЕНСКИЕ ИМЕНА (40 шт.)
        // Все имена вымышленные и не существуют в реальных языках.
        // =====================================================================
        private static readonly NameSet[] femaleNames = new NameSet[]
        {
            new NameSet("Хельнара",   "Хель",  "Хельна"),
            new NameSet("Геретна",    "Гере",  "Герет"),
            new NameSet("Лизбиет",    "Лиз",   "Лиззи"),
            new NameSet("Катриния",   "Кат",   "Катри"),
            new NameSet("Мирана",     "Мир",   "Мири"),
            new NameSet("Эларан",     "Эла",   "Элли"),
            new NameSet("Бриара",     "Бри",   "Бриа"),
            new NameSet("Каллистра",  "Калл",  "Калли"),
            new NameSet("Дарьела",    "Дар",   "Дари"),
            new NameSet("Фьора",      "Фьо",   "Фьори"),
            new NameSet("Гретина",    "Гре",   "Грети"),
            new NameSet("Изольдра",   "Изо",   "Иззи"),
            new NameSet("Улиена",     "Ули",   "Ули"),
            new NameSet("Кэтриэла",   "Кэт",   "Кэти"),
            new NameSet("Нарида",     "Над",   "Нади"),
            new NameSet("Одеррия",    "Оде",   "Оди"),
            new NameSet("Лиран",      "Лир",   "Лири"),
            new NameSet("Парла",      "Пар",   "Парли"),
            new NameSet("Кавина",     "Кав",   "Кави"),
            new NameSet("Серафен",    "Сера",  "Сери"),
            new NameSet("Авелина",    "Аве",   "Ави"),
            new NameSet("Береника",   "Бер",   "Бери"),
            new NameSet("Вивьена",    "Вив",   "Виви"),
            new NameSet("Глориана",   "Гло",   "Глори"),
            new NameSet("Дезирея",    "Дез",   "Дези"),
            new NameSet("Женева",     "Жен",   "Жени"),
            new NameSet("Ильвира",    "Иль",   "Ильви"),
            new NameSet("Кларисса",   "Клар",  "Клари"),
            new NameSet("Лоретта",    "Лор",   "Лори"),
            new NameSet("Марселла",   "Марс",  "Марси"),
            new NameSet("Октавия",    "Окта",  "Окти"),
            new NameSet("Петрунелла", "Пет",   "Петти"),
            new NameSet("Фредерика",  "Фред",  "Фреди"),
            new NameSet("Целестина",  "Целе",  "Цели"),
            new NameSet("Терезия",    "Тер",   "Тери"),
            new NameSet("Урсула",     "Урс",   "Урси"),
            new NameSet("Виолетта",   "Вио",   "Виоли"),
            new NameSet("Розалин",    "Роз",   "Рози"),
            new NameSet("Стеллара",   "Стел",  "Стелли"),
            new NameSet("Эльзара",    "Эль",   "Эльзи"),
        };

        // =====================================================================
        // ФАМИЛИИ (40 шт.)
        // Искажённые немецко-скандинавские фамилии, не существующие в реальности.
        // =====================================================================
        private static readonly string[] surnames = new string[]
        {
            "Мюллар", "Шмильт", "Шнайдар", "Фишар", "Вебен",
            "Вагнар", "Беккен", "Хоффмар", "Шефен", "Кохен",
            "Баулар", "Рихтар", "Клайнен", "Вольфар", "Шрёдар",
            "Ноймар", "Шварцен", "Циммермар", "Браунен", "Крюген",
            "Хартмар", "Ланген", "Вернар", "Краузар", "Мейар",
            "Лемар", "Шульцар", "Майар", "Кёлар", "Херрмар",
            "Кёнигар", "Вальтар", "Хубар", "Кайзар", "Фуксен",
            "Петерсан", "Циглар", "Дорн", "Эссерн", "Йенссен"
        };

        // =====================================================================
        // МУЖСКИЕ ВТОРЫЕ ИМЕНА / ПАТРОНИМЫ (40 шт.)
        // Стили: зан (10), сор (8), лу (8), ар (8), none (6).
        // Стиль None — мужское имя без суффикса ("имя отца").
        // =====================================================================
        private static readonly string[] patronymicsMale = new string[]
        {
            // Стиль Zan (10)
            "Тьядорзан", "Харрьезан", "Киентинзан", "Паксельзан", "Лотарьезан",
            "Геретнзан", "Эвратзан", "Корванзан", "Элиантзан", "Нолассзан",
            // Стиль Sor (8)
            "Тьядсор", "Харрсор", "Киентсор", "Пакссор", "Лотарсор",
            "Геретсор", "Эвратсор", "Корвансор",
            // Стиль Lu (8)
            "Тьялу", "Харлу", "Киенлу", "Паклу", "Лоталу",
            "Герелу", "Эвралу", "Корвалу",
            // Стиль Ar (8)
            "Тьядар", "Харрар", "Киентар", "Паксар", "Лотарар",
            "Геретар", "Эвратар", "Корванар",
            // Стиль None (6) — просто мужское имя
            "Тьядор", "Харрье", "Киентин", "Паксель", "Лотарье", "Геретн"
        };

        // =====================================================================
        // ЖЕНСКИЕ ВТОРЫЕ ИМЕНА / ПАТРОНИМЫ (40 шт.)
        // Стили: занна (10), сора (8), ла (8), ара (8), none (6).
        // Стиль None — то же мужское имя без изменений.
        // =====================================================================
        private static readonly string[] patronymicsFemale = new string[]
        {
            // Стиль Zan (10)
            "Тьядорзанна", "Харрьезанна", "Киентинзанна", "Паксельзанна", "Лотарьезанна",
            "Геретнзанна", "Эвратзанна", "Корванзанна", "Элиантзанна", "Нолассзанна",
            // Стиль Sor (8)
            "Тьядсора", "Харрсора", "Киентсора", "Пакссора", "Лотарсора",
            "Геретсора", "Эвратсора", "Корвансора",
            // Стиль Lu (8)
            "Тьяла", "Харла", "Киенла", "Пакла", "Лотала",
            "Герела", "Эврала", "Корвала",
            // Стиль Ar (8)
            "Тьядара", "Харрара", "Киентара", "Паксара", "Лотарара",
            "Геретара", "Эвратара", "Корванара",
            // Стиль None (6) — без суффикса, то же мужское имя
            "Тьядор", "Харрье", "Киентин", "Паксель", "Лотарье", "Геретн"
        };

        // =====================================================================
        // EN-ЛОКАЛИЗАЦИЯ (закомментировано — готово к будущему включению).
        // Примеры шаблонов и приставок.
        // =====================================================================
        /*
        private static readonly (string prefix, string suffix)[] enElderPrefixes =
        {
            ("Master ", ""),
        };

        private const string EN_JANITOR_PREFIX = "Master ";

        // Имена можно держать в отдельных списках enMaleNames / enFemaleNames
        // и переключать через #if UNITY_EN_LOCALIZATION.
        */

        // =====================================================================
        // API
        // =====================================================================

        /// <summary>Случайный мужской NameSet.</summary>
        public static NameSet GetRandomMaleName() => maleNames[UnityEngine.Random.Range(0, maleNames.Length)];

        /// <summary>Случайный женский NameSet.</summary>
        public static NameSet GetRandomFemaleName() => femaleNames[UnityEngine.Random.Range(0, femaleNames.Length)];

        /// <summary>Случайный NameSet по полу.</summary>
        public static NameSet GetRandomName(Gender gender)
        {
            return gender == Gender.Male ? GetRandomMaleName() : GetRandomFemaleName();
        }

        /// <summary>Случайная фамилия.</summary>
        public static string GetRandomSurname() => surnames[UnityEngine.Random.Range(0, surnames.Length)];

        /// <summary>Случайный патроним (второе имя) по полу.</summary>
        public static string GetRandomPatronymic(Gender gender)
        {
            var pool = gender == Gender.Male ? patronymicsMale : patronymicsFemale;
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        /// <summary>Готовая структура StaffNameData для сотрудника.</summary>
        public static StaffController.StaffNameData BuildStaffName(Gender gender)
        {
            var name = GetRandomName(gender);
            string surname = GetRandomSurname();

            // Лёгкая феминизация фамилии (только если оканчивается на согласную)
            if (gender == Gender.Female)
            {
                char last = surname.Length > 0 ? surname[surname.Length - 1] : ' ';
                if (last != 'а' && last != 'я' && last != 'ь')
                {
                    surname += "а";
                }
            }

            return new StaffController.StaffNameData
            {
                firstName = name.full,
                shortName = name.shortName,
                diminutiveName = name.diminutive,
                lastName = surname,
                patronymic = GetRandomPatronymic(gender)
            };
        }

        /// <summary>Готовое имя клиента (имя + фамилия, без патронима).</summary>
        public static string BuildClientFullName(Gender gender, out NameSet name, out string surname)
        {
            name = GetRandomName(gender);
            surname = GetRandomSurname();

            if (gender == Gender.Female)
            {
                char last = surname.Length > 0 ? surname[surname.Length - 1] : ' ';
                if (last != 'а' && last != 'я' && last != 'ь')
                {
                    surname += "а";
                }
            }

            return $"{name.full} {surname}";
        }
    }
}