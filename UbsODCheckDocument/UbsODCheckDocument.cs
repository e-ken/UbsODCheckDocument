using System;
using System.Collections.Generic;
using System.Text;

using UbsService;
using System.Text.RegularExpressions;
using System.Globalization;

namespace UbsBusiness {

    class StringLenComparer : IComparer<string> {
        public int Compare(string x, string y) {

            string xx = x ?? string.Empty;
            string yy = y ?? string.Empty;

            int result = xx.Length.CompareTo(yy.Length);
            if (result == 0) result = string.Compare(xx, yy, true);
            return result * -1;
        }
    }

    /// <summary>
    /// Класс проверки документов
    /// </summary>
    public partial class UbsODCheckDocument {
        
        private static string allowableDigChars = "0123456789";
        private static string allowableChars = "0123456789АБВГДЕЖИКЛМНОПРСТУФЦЧШЩЭЮЯDFGIJLNQRSUVWYZ";
        private static string allowablePaymentChars = " !\"#№$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя"; // «»
        private static DateTime dt22220101 = new DateTime(2222, 1, 1);
        private static DateTime dt19900101 = new DateTime(1990, 1, 1);
        
        private static readonly NumberFormatInfo formatCurrency2 = new NumberFormatInfo() {
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = "-",
            CurrencyGroupSeparator = "",
            CurrencySymbol = "",
            NumberDecimalSeparator = "."
        };


        #region Переменные установки
        /// <summary>
        /// Идентификатор базовой валюты
        /// </summary>
        public readonly short settingIdBaseCurrency;
        /// <summary>
        /// Установка: БИК банка
        /// </summary>
        public readonly string settingBicBank = null;
        /// <summary>
        /// Установка: ИНН банка
        /// </summary>
        public readonly string settingInnBank = null;
        /// <summary>
        /// Установка: КПП банка
        /// </summary>
        public readonly string settingKppBank = null;
        /// <summary>
        /// Коррсчет банка
        /// </summary>
        public readonly string settingCorrAccountBank = null;
        /// <summary>
        /// Проверять по справочнику разрешенных счетов
        /// </summary>
        public readonly bool settingCheckApprovedBal2 = false;
        /// <summary>
        /// Установка: Наименование банка
        /// </summary>
        public readonly string settingNameBank = null;
        /// <summary>
        /// Установка: Наименование плательщика/получателя для ПД
        /// </summary>
        public readonly string settingNamePaymentDocument = null;
        /// <summary>
        /// Установка: Наименование филиала
        /// </summary>
        public readonly string settingNameBranch = null;
        /// <summary>
        /// Установка: Наименование филиала (полное)
        /// </summary>
        public readonly string settingNameBranchFull = null;

        private readonly int settingRegimIdentAccount;
        private readonly int settingSaveAccountBlockFreaze;
        private readonly int settingTypeCheckDocumentOnCardIndex2;
        private readonly bool settingCheckKpp = false;
        private readonly bool settingCheckUFK;
        private readonly bool settingCheckNote;
        private readonly bool settingIsNumerateIspr = false;
        private readonly bool settingIsCheckNumberDoc = false;
        private readonly bool settingNotCheckNotePayPT = false;
        private readonly bool settingIsCheckLengthNumberDoc = false;
        private readonly bool settingCheckTerroristActivitesNote = false;
        private readonly bool settingCheckTerroristActivitesPayerName = false;
        private readonly bool settingCheckTerroristActivitesConditionPay = false;
        private readonly bool settingCheckTerroristActivitesRecipientName = false;
        private readonly bool settingCheckTerroristActivitesBankRecipient = false;
        private readonly int[,] settingInnAllowableLen = null;
        private readonly string settingCodeRKC = null;
        private readonly string settingKindName;
        private readonly string settingOkatoBank = null;
        private readonly string settingAdditionalTextPayer = null;
        private readonly string settingSearchDuplicatePayment;
        private readonly decimal settingLimSummaAddInfoPayer = 0;
        private readonly DateTime settingDateOD;        
        private readonly string[] settingFormFillingInformationP0 = null, settingFormFillingInformationP1 = null;
        private readonly object[,] settingMaskAccountNFEC = null;
        private readonly object[,] settingNFECInNoteDocument = null;
        private readonly object[,] settingNFECInNoteDocumentExclude = null;
        private readonly List<string> settingСodesOfTheRegions = new List<string>();
        private readonly List<string> settingBal2AccountsNonResident = new List<string>();
        private readonly List<string> settingСodesOfCCT = new List<string>();
        private readonly List<string> settingBalAccClients = new List<string>();
        private readonly List<string> settingIsNeedClientActivity = new List<string>();
        private readonly List<string> settingBicAndAccountFTCRussia = new List<string>();
        private readonly List<string> settingDelimiter = new List<string>();
        private readonly Dictionary<string, bool> settingCheckTerroristFields = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> settingTaxFieldsCheck = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> settingTaxFieldsAllowableValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> settingDefaultNamePaymentRecipient = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> settingSearchNDSNotIncludeAcc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly SortedDictionary<string, string> settingAbbreviation = new SortedDictionary<string, string>(new StringLenComparer());

        #endregion

        private readonly IUbsWss ubs = null;
        private readonly IUbsDbConnection connection = null;
        private UbsODPayDoc document = null;
        private readonly UbsComLibrary ubsComLibrary = null;
        private readonly UbsODAccount ubsOdAccount = null;
        private readonly UbsComClient ubsComClient = null;
        private readonly UbsComRates ubsComRates = null;

        /// <summary>
        /// Создание экземпляра класса проверки документа
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложения</param>
        /// <param name="document">Платежный документ</param>
        [Obsolete("Следует использовать конструктор (IUbsDbConnection, IUbsWss), а для установки Документа метод Document", true)]
        public UbsODCheckDocument(IUbsDbConnection connection, IUbsWss ubs, UbsODPayDoc document) {
            this.ubs = ubs;
            this.connection = connection;
            this.document = document;
            this.ubsComLibrary = new UbsComLibrary(this.connection, this.ubs);
            this.ubsOdAccount = new UbsODAccount(this.connection, this.ubs, 0);
            this.ubsComClient = new UbsComClient(this.connection, this.ubs);
            this.ubsComRates = new UbsComRates(this.connection, this.ubs);

            #region Чтение установок

            this.connection.ClearParameters();
            this.connection.CmdText = "select VALUE_DATE from COM_BANK_DATE where NAME_DATE = 'Операционный день'";
            settingDateOD = (DateTime)this.connection.ExecuteScalar();

            // Основные установки                   Базовая валюта
            this.settingIdBaseCurrency = Convert.ToInt16(this.ubs.UbsWssParam("Установка", "Основные установки", "Базовая валюта"));
            if (this.settingIdBaseCurrency <= 0)
                throw new UbsObjectException("Установка <Основные установки-Базовая валюта> не заполнена");


            // Основные установки                   Условный номер кредитной организации
            //settingConditionNumberCreditOrganization = Convert.ToString(this.IUbsWss.UbsWssParam("Установка", new object[] { "Основные установки", "Условный номер кредитной организации" })).Trim();

            //Основные установки                    БИК банка
            this.settingBicBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "БИК банка")).Trim();

            //Основные установки                    ИНН банка
            settingInnBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "ИНН банка")).Trim();

            //Основные установки                    КППУ банка
            settingKppBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "КППУ банка")).Trim();

            //Основные установки                    ОКАТО банка
            settingOkatoBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "ОКАТО банка")).Trim();

            //Основные установки                    Коррсчет банка
            this.settingCorrAccountBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "Коррсчет банка")).Trim();

            // Основные установки                   Наименование банка
            settingNameBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "Наименование банка")).Trim();

            // Основные установки                   Наименование филиала
            settingNameBranch = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "Наименование филиала")).Trim();

            // Основные установки                   Наименование филиала (полное)
            settingNameBranchFull = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "Наименование филиала (полное)")).Trim();

            // Основные установки                   Наименование плательщика/получателя для ПД
            settingNamePaymentDocument = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "Наименование плательщика/получателя для ПД")).Trim();
            if (string.IsNullOrEmpty(settingNamePaymentDocument)) settingNamePaymentDocument = settingNameBranch; // Для полей в наименовании п/п

            // Операционный день                    Проверять номер документа
            settingIsCheckNumberDoc = "ДА".Equals(Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Проверять номер документа")).Trim(), StringComparison.OrdinalIgnoreCase);

            //Операционный день                     Проверять длину номера документа
            settingIsCheckLengthNumberDoc = Convert.ToInt32(this.ubs.UbsWssParam("Установка", "Операционный день", "Проверять длину номера документа")) > 0;

            // Операционный день                    Сохранять документы с замор./забл. счетами 0-нет, 1-да,  2-заблокиров.,  3-заморож.
            settingSaveAccountBlockFreaze = Convert.ToInt32(Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Сохранять документы с замор./забл. счетами")));

            // Операционный день                    Режим идентификации счета
            settingRegimIdentAccount = Convert.ToInt32(Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Режим идентификации счета")));

            // Операционный день                    Дополнение к наименованию плательщика
            settingAdditionalTextPayer = Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Дополнение к наименованию плательщика"));

            // Операционный день                    Пороговая сумма,треб.доп.инф.о плательщике
            object value = this.ubs.UbsWssParam("Установка", "Операционный день", "Пороговая сумма,треб.доп.инф.о плательщике");
            settingLimSummaAddInfoPayer = value == null ? (decimal)15000 : (decimal)value;

            // Операционный день                    Дополнять наим. пл. видом деят. физ. лица"
            object[] item1 = (object[])this.ubs.UbsWssParam("Установка", "Операционный день", "Дополнять наим. пл. видом деят. физ. лица");
            foreach (string item in item1) {
                string normalItem = (item ?? "").Trim();
                if (!string.IsNullOrEmpty(normalItem)) settingIsNeedClientActivity.Add(normalItem);
            }

            // Операционный день                       Проверять назначение платежа
            settingCheckNote = Convert.ToInt32(this.ubs.UbsWssParam("Установка", "Операционный день", "Проверять назначение платежа")) > 0;

            // Операционный день                    Налоговые поля: Допустимые значения
            object[,] item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Налоговые поля: Допустимые значения");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingTaxFieldsAllowableValues.ContainsKey(key)) settingTaxFieldsAllowableValues.Add(key, Convert.ToString(item2[1, i]));
            }

            //Операционный день                     Бал. счета определяющие нерезидентов
            item1 = (object[])this.ubs.UbsWssParam("Установка", new object[] { "Операционный день", "Бал. счета определяющие нерезидентов" });
            foreach (object item in item1)
                settingBal2AccountsNonResident.Add(Convert.ToString(item).Trim());

            // Операционный день                    Вид заполнения информации о плательщике
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Вид заполнения информации о плательщике");
            for (int i = 0; i <= item2.GetUpperBound(1); i++)
                if (Convert.ToInt32(item2[0, i]) == 1) {
                    settingFormFillingInformationP0 = Convert.ToString(item2[1, i]).Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    settingFormFillingInformationP1 = Convert.ToString(item2[2, i]).Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    for (int j = 0; j < settingFormFillingInformationP0.Length; j++) settingFormFillingInformationP0[j] = settingFormFillingInformationP0[j].Trim();
                    for (int j = 0; j < settingFormFillingInformationP1.Length; j++) settingFormFillingInformationP1[j] = settingFormFillingInformationP1[j].Trim();
                    break;
                }

            // Операционный день                     Исключаемые счета в пров. НДС
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Исключаемые счета в пров. НДС");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingSearchNDSNotIncludeAcc.ContainsKey(key)) settingSearchNDSNotIncludeAcc.Add(key, Convert.ToInt32(item2[1, i]));
            }

            // Операционный день                     Наименование плат./получ. по умолчанию
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Наименование плат./получ. по умолчанию");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingDefaultNamePaymentRecipient.ContainsKey(key)) settingDefaultNamePaymentRecipient.Add(key, Convert.ToInt32(item2[1, i]));
            }


            // Операционный день                    Налоговые поля: Проверять
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Налоговые поля: Проверять");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingTaxFieldsCheck.ContainsKey(key)) settingTaxFieldsCheck.Add(key, Convert.ToBoolean(item2[1, i]));
            }

            //Операционный день                     Проверка документа по словарю террористов
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Проверка документа по словарю террористов");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingCheckTerroristFields.ContainsKey(key)) settingCheckTerroristFields.Add(key, Convert.ToBoolean(item2[1, i]));
            }

            //Операционный день                     Налоговые поля: БИК,Счет (ФТС России)
            item1 = (object[])this.ubs.UbsWssParam("Установка", "Операционный день", "Налоговые поля: БИК,Счет (ФТС России)");
            for (int i = 0; i < item1.Length; i++) {
                settingBicAndAccountFTCRussia.Add(Convert.ToString(item1[i]).Trim());
            }


            //Операционный день                     ИНН - допустимая длина
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "ИНН - допустимая длина");
            settingInnAllowableLen = new int[2, item2.GetUpperBound(1) + 1];
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                settingInnAllowableLen[0, i] = Convert.ToInt32(item2[0, i]);
                settingInnAllowableLen[1, i] = Convert.ToInt32(item2[1, i]);
            }

            //Операционный день                     Список банков для проверки
            //object[] item1 = (object[])this.IUbsWss.UbsWssParam("Установка", new object[] { "Операционный день", "Список банков для проверки" });
            //foreach (object item in item1)
            //    settingBicsForCheck.Add(Convert.ToString(item).Trim());

            //Операционный день                     Необяз. проверки назн. платежа в ПТ
            settingNotCheckNotePayPT = Convert.ToInt32(Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Необяз. проверки назн. платежа в ПТ"))) == 1;

            //Операционный день                     Коды видов валютных операций
            this.connection.ClearParameters();
            this.connection.CmdText = "select CODE_OPERATION from OD_LIST_CVVO order by CODE_OPERATION asc";
            this.connection.ExecuteUbsDbReader();
            while (this.connection.Read()) settingСodesOfCCT.Add(this.connection.GetString(0));
            this.connection.CloseReader();
            this.connection.CmdReset();

            // Операционный день                    Банковские счета и счета вкладов клиентов
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Банковские счета и счета вкладов клиентов");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key)) settingBalAccClients.Add(key);
            }

            //Операционный день    КВВО в документе
            settingMaskAccountNFEC = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "КВВО в документе");

            //Операционный день    КВВО в назначении платежа документов
            settingNFECInNoteDocument = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "КВВО в назначении платежа документов");

            //Операционный день    КВВО в назначении платежа документов искл
            settingNFECInNoteDocumentExclude = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "КВВО в назначении платежа документов искл");

            // Операционный день    Проверять КПП в платежном документе
            this.settingCheckKpp = "ДА".Equals((string)this.ubs.UbsWssParam("Установка", "Операционный день", "Проверять КПП в платежном документе"), StringComparison.OrdinalIgnoreCase);


            // Операционный день    Режим нумерации исправительных МО
            this.settingIsNumerateIspr = Convert.ToInt32(Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Режим нумерации исправительных МО"))) == 1;

            // Операционный день    Проверка документа по словарю террористов
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Проверка документа по словарю террористов");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                if (Convert.ToInt32(item2[1, i]) > 0) {
                    string key = Convert.ToString(item2[0, i]).Trim();
                    if ("Наименование плательщика".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesPayerName = true; continue; }
                    if ("Наименование получателя".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesRecipientName = true; continue; }
                    if ("Назначение платежа".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesNote = true; continue; }
                    if ("Условие оплаты".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesConditionPay = true; continue; }
                    //if ("Наименование банка плательщика".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesBankPayer = true; continue; }
                    if ("Наименование банка получателя".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesBankRecipient = true; continue; }
                }
            }

            this.connection.ClearParameters();
            this.connection.CmdText = "select b.ID_BUSINESS from UBS_BUSINESS b where b.COD_BUSINESS = @COD_BUSINESS";
            this.connection.AddInputParameter("COD_BUSINESS", System.Data.SqlDbType.VarChar, "RC");
            object scalar = this.connection.ExecuteScalar();
            this.connection.CmdReset();
            if (scalar != null) {
                // Расчетный центр                      Коды территорий региона
                item1 = (object[])this.ubs.UbsWssParam("Установка", "Расчетный центр", "Коды территорий региона");
                foreach (object item in item1) {
                    string key = Convert.ToString(item).Trim();
                    settingСodesOfTheRegions.Add(key);
                }

                item2 = (object[,])this.ubs.UbsWssParam("Установка", "Расчетный центр", "Параметры проверки создаваемых платежей");
                for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                    string key = Convert.ToString(item2[0, i]).Trim();
                    if ("Проверка по справочнику разрешенных счетов (1- Да / 0 - Нет)".Equals(key, StringComparison.OrdinalIgnoreCase)) {
                        int result;
                        if (int.TryParse(Convert.ToString(item2[1, i]).Trim(), out result) && result == 1) this.settingCheckApprovedBal2 = true;
                        break;
                    }
                }

                this.settingCodeRKC = Convert.ToString(this.ubs.UbsWssParam("Установка", "Расчетный центр", "РКЦ")).Trim();

                item2 = (object[,])this.ubs.UbsWssParam("Установка", "Расчетный центр", "Параметры поиска дублирующих платежей");
                for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                    string key = Convert.ToString(item2[0, i]).Trim();
                    if ("Используемые направления платежа (строка пять символов 0 или 1, 1 - использовать, 0 - не использовать)".Equals(key, StringComparison.OrdinalIgnoreCase)) {

                        this.settingSearchDuplicatePayment = Convert.ToString(item2[1, i]).Trim();
                        if (string.IsNullOrEmpty(this.settingSearchDuplicatePayment)) this.settingSearchDuplicatePayment = "11111";
                        if (this.settingSearchDuplicatePayment.Length > 5) this.settingSearchDuplicatePayment = this.settingSearchDuplicatePayment.Substring(0, 5);
                        break;
                    }
                }
            }

            settingTypeCheckDocumentOnCardIndex2 = Convert.ToInt32(this.ubs.UbsWssParam("Установка", "Операционный день", "Квитовка - проверять док. клиента на карт."));

            // Операционный день Сверка наименования клиента.Разделитель"
            item1 = (object[])this.ubs.UbsWssParam("Установка", "Операционный день", "Сверка наименования клиента.Разделитель");
            foreach (object item in item1) {
                string key = Convert.ToString(item).Trim();
                if (!string.IsNullOrEmpty(key)) settingDelimiter.Add(key);
            }

            // Операционный день Сверка наименования клиента.Аббревиатуры"
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Сверка наименования клиента.Аббревиатуры");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingAbbreviation.ContainsKey(key)) settingAbbreviation.Add(key, Convert.ToString(item2[1, i]).Trim());
            }

            // Операционный день Проверять КФХ в назначении получателя
            this.settingCheckUFK = Convert.ToInt32(this.ubs.UbsWssParam("Установка", "Операционный день", "Проверять УФК в наименовании получателя")) > 0;

            #endregion
        }

        /// <summary>
        /// Создание экземпляра класса проверки нескольких документов, документ устанавливается через свойство объекта Document
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложения</param>
        public UbsODCheckDocument(IUbsDbConnection connection, IUbsWss ubs) {
            this.ubs = ubs;
            this.connection = connection;
            this.ubsComLibrary = new UbsComLibrary(this.connection, this.ubs);
            this.ubsOdAccount = new UbsODAccount(this.connection, this.ubs, 0);
            this.ubsComClient = new UbsComClient(this.connection, this.ubs);
            this.ubsComRates = new UbsComRates(this.connection, this.ubs);

            #region Чтение установок

            this.connection.ClearParameters();
            this.connection.CmdText = "select VALUE_DATE from COM_BANK_DATE where NAME_DATE = 'Операционный день'";
            settingDateOD = (DateTime)this.connection.ExecuteScalar();

            // Основные установки                   Базовая валюта
            this.settingIdBaseCurrency = Convert.ToInt16(this.ubs.UbsWssParam("Установка", "Основные установки", "Базовая валюта"));
            if (this.settingIdBaseCurrency <= 0)
                throw new UbsObjectException("Установка <Основные установки-Базовая валюта> не заполнена");


            // Основные установки                   Условный номер кредитной организации
            //settingConditionNumberCreditOrganization = Convert.ToString(this.IUbsWss.UbsWssParam("Установка", new object[] { "Основные установки", "Условный номер кредитной организации" })).Trim();

            //Основные установки                    БИК банка
            this.settingBicBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "БИК банка")).Trim();

            //Основные установки                    ИНН банка
            settingInnBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "ИНН банка")).Trim();

            //Основные установки                    КППУ банка
            settingKppBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "КППУ банка")).Trim();

            //Основные установки                    ОКАТО банка
            settingOkatoBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "ОКАТО банка")).Trim();

            //Основные установки                    Коррсчет банка
            this.settingCorrAccountBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "Коррсчет банка")).Trim();

            // Основные установки                   Наименование банка
            settingNameBank = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "Наименование банка")).Trim();

            // Основные установки                   Наименование филиала
            settingNameBranch = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "Наименование филиала")).Trim();

            // Основные установки                   Наименование филиала (полное)
            settingNameBranchFull = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "Наименование филиала (полное)")).Trim();

            // Основные установки                   Наименование плательщика/получателя для ПД
            settingNamePaymentDocument = Convert.ToString(this.ubs.UbsWssParam("Установка", "Основные установки", "Наименование плательщика/получателя для ПД")).Trim();
            if (string.IsNullOrEmpty(settingNamePaymentDocument)) settingNamePaymentDocument = settingNameBranch; // Для полей в наименовании п/п

            // Операционный день                    Проверять номер документа
            settingIsCheckNumberDoc = "ДА".Equals(Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Проверять номер документа")).Trim(), StringComparison.OrdinalIgnoreCase);

            //Операционный день                     Проверять длину номера документа
            settingIsCheckLengthNumberDoc = Convert.ToInt32(this.ubs.UbsWssParam("Установка", "Операционный день", "Проверять длину номера документа")) > 0;

            // Операционный день                    Сохранять документы с замор./забл. счетами 0-нет, 1-да,  2-заблокиров.,  3-заморож.
            settingSaveAccountBlockFreaze = Convert.ToInt32(Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Сохранять документы с замор./забл. счетами")));

            // Операционный день                    Режим идентификации счета
            settingRegimIdentAccount = Convert.ToInt32(Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Режим идентификации счета")));

            // Операционный день                    Дополнение к наименованию плательщика
            settingAdditionalTextPayer = Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Дополнение к наименованию плательщика"));

            // Операционный день                    Пороговая сумма,треб.доп.инф.о плательщике
            object value = this.ubs.UbsWssParam("Установка", "Операционный день", "Пороговая сумма,треб.доп.инф.о плательщике");
            settingLimSummaAddInfoPayer = value == null ? (decimal)15000 : (decimal)value;

            // Операционный день                    Дополнять наим. пл. видом деят. физ. лица"
            object[] item1 = (object[])this.ubs.UbsWssParam("Установка", "Операционный день", "Дополнять наим. пл. видом деят. физ. лица");
            foreach (string item in item1) {
                string normalItem = (item ?? "").Trim();
                if (!string.IsNullOrEmpty(normalItem)) settingIsNeedClientActivity.Add(normalItem);
            }

            // Операционный день                       Проверять назначение платежа
            settingCheckNote = Convert.ToInt32(this.ubs.UbsWssParam("Установка", "Операционный день", "Проверять назначение платежа")) > 0;

            // Операционный день                    Налоговые поля: Допустимые значения
            object[,] item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Налоговые поля: Допустимые значения");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingTaxFieldsAllowableValues.ContainsKey(key)) settingTaxFieldsAllowableValues.Add(key, Convert.ToString(item2[1, i]));
            }

            //Операционный день                     Бал. счета определяющие нерезидентов
            item1 = (object[])this.ubs.UbsWssParam("Установка", new object[] { "Операционный день", "Бал. счета определяющие нерезидентов" });
            foreach (object item in item1)
                settingBal2AccountsNonResident.Add(Convert.ToString(item).Trim());

            // Операционный день                    Вид заполнения информации о плательщике
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Вид заполнения информации о плательщике");
            for (int i = 0; i <= item2.GetUpperBound(1); i++)
                if (Convert.ToInt32(item2[0, i]) == 1) {
                    settingFormFillingInformationP0 = Convert.ToString(item2[1, i]).Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    settingFormFillingInformationP1 = Convert.ToString(item2[2, i]).Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    for (int j = 0; j < settingFormFillingInformationP0.Length; j++) settingFormFillingInformationP0[j] = settingFormFillingInformationP0[j].Trim();
                    for (int j = 0; j < settingFormFillingInformationP1.Length; j++) settingFormFillingInformationP1[j] = settingFormFillingInformationP1[j].Trim();
                    break;
                }

            // Операционный день                     Исключаемые счета в пров. НДС
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Исключаемые счета в пров. НДС");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingSearchNDSNotIncludeAcc.ContainsKey(key)) settingSearchNDSNotIncludeAcc.Add(key, Convert.ToInt32(item2[1, i]));
            }

            // Операционный день                     Наименование плат./получ. по умолчанию
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Наименование плат./получ. по умолчанию");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingDefaultNamePaymentRecipient.ContainsKey(key)) settingDefaultNamePaymentRecipient.Add(key, Convert.ToInt32(item2[1, i]));
            }


            // Операционный день                    Налоговые поля: Проверять
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Налоговые поля: Проверять");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingTaxFieldsCheck.ContainsKey(key)) settingTaxFieldsCheck.Add(key, Convert.ToBoolean(item2[1, i]));
            }

            //Операционный день                     Проверка документа по словарю террористов
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Проверка документа по словарю террористов");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingCheckTerroristFields.ContainsKey(key)) settingCheckTerroristFields.Add(key, Convert.ToBoolean(item2[1, i]));
            }

            //Операционный день                     Налоговые поля: БИК,Счет (ФТС России)
            item1 = (object[])this.ubs.UbsWssParam("Установка", "Операционный день", "Налоговые поля: БИК,Счет (ФТС России)");
            for (int i = 0; i < item1.Length; i++) {
                settingBicAndAccountFTCRussia.Add(Convert.ToString(item1[i]).Trim());
            }

            //Операционный день                     ИНН - допустимая длина
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "ИНН - допустимая длина");
            settingInnAllowableLen = new int[2, item2.GetUpperBound(1) + 1];
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                settingInnAllowableLen[0, i] = Convert.ToInt32(item2[0, i]);
                settingInnAllowableLen[1, i] = Convert.ToInt32(item2[1, i]);
            }

            //Операционный день                     Список банков для проверки
            //object[] item1 = (object[])this.IUbsWss.UbsWssParam("Установка", new object[] { "Операционный день", "Список банков для проверки" });
            //foreach (object item in item1)
            //    settingBicsForCheck.Add(Convert.ToString(item).Trim());

            //Операционный день                     Необяз. проверки назн. платежа в ПТ
            settingNotCheckNotePayPT = Convert.ToInt32(Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Необяз. проверки назн. платежа в ПТ"))) == 1;

            //Операционный день                     Коды видов валютных операций
            this.connection.ClearParameters();
            this.connection.CmdText = "select CODE_OPERATION from OD_LIST_CVVO order by CODE_OPERATION asc";
            this.connection.ExecuteUbsDbReader();
            while (this.connection.Read()) settingСodesOfCCT.Add(this.connection.GetString(0));
            this.connection.CloseReader();
            this.connection.CmdReset();

            // Операционный день                    Банковские счета и счета вкладов клиентов

            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Банковские счета и счета вкладов клиентов");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key)) settingBalAccClients.Add(key);
            }

            this.settingKindName = ((string)this.ubs.UbsWssParam("Установка", "Операционный день", "Вид наименования плательщика"));

            //Операционный день    КВВО в документе
            settingMaskAccountNFEC = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "КВВО в документе");

            //Операционный день    КВВО в назначении платежа документов
            settingNFECInNoteDocument = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "КВВО в назначении платежа документов");

            //Операционный день    КВВО в назначении платежа документов искл
            settingNFECInNoteDocumentExclude = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "КВВО в назначении платежа документов искл");

            // Операционный день    Проверять КПП в платежном документе
            this.settingCheckKpp = "ДА".Equals((string)this.ubs.UbsWssParam("Установка", "Операционный день", "Проверять КПП в платежном документе"), StringComparison.OrdinalIgnoreCase);

            // Операционный день    Режим нумерации исправительных МО
            this.settingIsNumerateIspr = Convert.ToInt32(Convert.ToString(this.ubs.UbsWssParam("Установка", "Операционный день", "Режим нумерации исправительных МО"))) == 1;

            // Операционный день    Проверка документа по словарю террористов
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Проверка документа по словарю террористов");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                if(Convert.ToInt32(item2[1, i]) > 0) {
                    string key = Convert.ToString(item2[0, i]).Trim();
                    if ("Наименование плательщика".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesPayerName = true; continue;}
                    if ("Наименование получателя".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesRecipientName = true; continue;}
                    if ("Назначение платежа".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesNote = true; continue;}
                    if ("Условие оплаты".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesConditionPay = true; continue;}
                    //if ("Наименование банка плательщика".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesBankPayer = true; continue; }
                    if ("Наименование банка получателя".Equals(key, StringComparison.OrdinalIgnoreCase)) { this.settingCheckTerroristActivitesBankRecipient = true; continue; }
                }
            }

            this.connection.ClearParameters();
            this.connection.CmdText = "select b.ID_BUSINESS from UBS_BUSINESS b where b.COD_BUSINESS = @COD_BUSINESS";
            this.connection.AddInputParameter("COD_BUSINESS", System.Data.SqlDbType.VarChar, "RC");
            object scalar = this.connection.ExecuteScalar();
            this.connection.CmdReset();
            if (scalar != DBNull.Value && scalar != null) {
                // Расчетный центр                      Коды территорий региона
                item1 = (object[])this.ubs.UbsWssParam("Установка", "Расчетный центр", "Коды территорий региона");
                foreach (object item in item1) {
                    string key = Convert.ToString(item).Trim();
                    settingСodesOfTheRegions.Add(key);
                }

                item2 = (object[,])this.ubs.UbsWssParam("Установка", "Расчетный центр", "Параметры проверки создаваемых платежей");
                for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                    string key = Convert.ToString(item2[0, i]).Trim();
                    if ("Проверка по справочнику разрешенных счетов (1- Да / 0 - Нет)".Equals(key, StringComparison.OrdinalIgnoreCase)) {
                        int result;
                        if (int.TryParse(Convert.ToString(item2[1, i]).Trim(), out result) && result == 1) this.settingCheckApprovedBal2 = true;
                        break;
                    }
                }

                item2 = (object[,])this.ubs.UbsWssParam("Установка", "Расчетный центр", "Параметры поиска дублирующих платежей");
                for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                    string key = Convert.ToString(item2[0, i]).Trim();
                    if ("Используемые направления платежа (строка пять символов 0 или 1, 1 - использовать, 0 - не использовать)".Equals(key, StringComparison.OrdinalIgnoreCase)) {

                        this.settingSearchDuplicatePayment = Convert.ToString(item2[1, i]).Trim();
                        if (string.IsNullOrEmpty(this.settingSearchDuplicatePayment)) this.settingSearchDuplicatePayment = "11111";
                        if (this.settingSearchDuplicatePayment.Length > 5) this.settingSearchDuplicatePayment = this.settingSearchDuplicatePayment.Substring(0, 5);
                        break;
                    }
                }
            }

            settingTypeCheckDocumentOnCardIndex2 = Convert.ToInt32(this.ubs.UbsWssParam("Установка", "Операционный день", "Квитовка - проверять док. клиента на карт."));


            // Операционный день Сверка наименования клиента.Разделитель"
            item1 = (object[])this.ubs.UbsWssParam("Установка", "Операционный день", "Сверка наименования клиента.Разделитель");
            foreach (object item in item1) {
                string key = Convert.ToString(item).Trim();
                if (!string.IsNullOrEmpty(key)) settingDelimiter.Add(key);
            }

            // Операционный день Сверка наименования клиента.Аббревиатуры"
            item2 = (object[,])this.ubs.UbsWssParam("Установка", "Операционный день", "Сверка наименования клиента.Аббревиатуры");
            for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                string key = Convert.ToString(item2[0, i]).Trim();
                if (!string.IsNullOrEmpty(key) && !settingAbbreviation.ContainsKey(key)) settingAbbreviation.Add(key, Convert.ToString(item2[1, i]).Trim());
            }

            // Операционный день Проверять КФХ в назначении получателя
            this.settingCheckUFK = Convert.ToInt32(this.ubs.UbsWssParam("Установка", "Операционный день", "Проверять УФК в наименовании получателя")) > 0;
                        
            #endregion
        }

        /// <summary>
        /// Установить новый документ
        /// </summary>
        public UbsODPayDoc Document { set { this.document = value; } }


        private static bool CheckChars(string value, string allowableString, bool canEmpty, out string message) {
            message = string.Empty;
            if (string.IsNullOrEmpty(value)) return canEmpty;

            for(int i = 0; i < value.Length; i++) {
                string c = value[i].ToString();
                if (!allowableString.Contains(c)) {
                    message = ": недопустимый символ <" + c + ">";
                    return false;
                }
            }
            return true;
        }

        private static bool IsContains(string source, string value) {
            if (string.IsNullOrEmpty(source)) return false;
            foreach (string item in source.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (item.Trim().Equals(value, StringComparison.Ordinal)) return true;

            return false;
        }
        /// <summary>
        /// Определяет внутренний документ или внешний при вводе платежного документа:
        /// Если установка Операционный день -> Режим идентификации счета:
        /// 0 – то внешний или внутренний документ определяется по совпадению БИКа получателя и плательщика.
        /// 1..4 – то кроме БИК-ов в анализе участвует код отделения, взятый из счета получателя,
        /// начиная с 13 позиции влево на количество символов, определяемым значением установки.
        /// Полученный код ищется в списке отделений и если находится, то документ считается внутренним иначе внешним.
        /// 5.. - то признак документа внутренний или внешний будет определяться исходя из наличия записи в таблице
        /// RC_ROUTE если запись для заданного БИК и кода отделения вырезанного из счета (4 позиции начиная с 10) 
        /// будет найдена в таблице то документ будет считаться внешним иначе внутренним.
        /// </summary>
        /// <returns>true - внутренний, false - внешний</returns>
        /// <exception cref="ArgumentException">БИК банка плательщика или получателя пусты, номер счета получателя не задан</exception>
        public bool IsLocate(string bicDb, string bicCr, string numAccountCr) {
            if (string.IsNullOrEmpty(bicDb)) throw new ArgumentException("БИК банка плательщика не задан");
            if (string.IsNullOrEmpty(bicCr)) throw new ArgumentException("БИК банка получателя не задан");

            if (!bicDb.Equals(bicCr, StringComparison.OrdinalIgnoreCase)) return false;

            if (string.IsNullOrEmpty(numAccountCr) || "00000000000000000000".Equals(numAccountCr)) return true;

            if (settingRegimIdentAccount >= 1 && settingRegimIdentAccount <= 4) {
              //  if (string.IsNullOrEmpty(numAccountCr)) throw new ArgumentException("Номер счета получателя не задан");

                string codeInAcc = numAccountCr.Substring(13 - settingRegimIdentAccount, settingRegimIdentAccount);

                this.connection.ClearParameters();
                this.connection.CmdText =
                    string.Format("select CODE_IN_ACC from UBS_DIVISION where NUM_DIVISION > 0 and CODE_IN_ACC like '%{0}'", codeInAcc);
                return (this.connection.ExecuteReadFirstRec() != null);
            }
            
            //if (string.IsNullOrEmpty(numAccountCr)) throw new ArgumentException("Номер счета получателя не задан");

            this.connection.ClearParameters();
            this.connection.CmdText =
                "select ID_ROUTE from RC_ROUTE where BIC = @BIC and CODE_FILIAL = @CODE_FILIAL";
            this.connection.AddInputParameter("BIC", bicCr);
            this.connection.AddInputParameter("CODE_FILIAL", numAccountCr.Substring(9, 4));
            return (this.connection.ExecuteScalar() == null);
            
            throw new UbsObjectException(string.Format("Установка 'Операционный день -> Режим идентификации счета' содержит неподдерживаемое значение '{0}'", settingRegimIdentAccount));
        }

        /// <summary>
        /// Проверка ключа счета
        /// </summary>
        /// <param name="bicAccount">БИК</param>
        /// <param name="numAccount">Номер счета</param>
        /// <returns>true - проверка успешна</returns>
        public static bool CheckKeyAccount(string bicAccount, string numAccount) {
            bool isRKC;
            if (bicAccount.Length != 9 || numAccount.Length != 20) return false;

            // 040486002 РКЦ - это организации, имеющие в БИК 00 в позициях 7,8 (первые две из последних трех
            isRKC = "00".Equals(bicAccount.Substring(6, 2), StringComparison.OrdinalIgnoreCase);
            return CheckKeyAccount(bicAccount, numAccount, isRKC);
        }


        /// <summary>
        /// Проверка ключа счета
        /// </summary>
        /// <param name="bicAccount">БИК</param>
        /// <param name="numAccount">Номер счета</param>
        /// <param name="isRKC">Признак счета: счет открыт в РКЦ (true), счет открыт в КБ (false)</param>
        /// <returns>true - проверка успешна</returns>
        public static bool CheckKeyAccount(string bicAccount, string numAccount, bool isRKC) {
            if (string.IsNullOrEmpty(bicAccount) || string.IsNullOrEmpty(numAccount)) return false;
            if (bicAccount.Length != 9 || numAccount.Length != 20) return false;

            numAccount = numAccount.Replace('A', '0').Replace('B', '1')
                                     .Replace('C', '2').Replace('E', '3')
                                     .Replace('Н', '4').Replace('K', '5')
                                     .Replace('M', '6').Replace('P', '7')
                                     .Replace('T', '8').Replace('X', '9');

            char[] chars = numAccount.Replace('A', '0').ToCharArray();
            string message;
            if (!CheckChars(numAccount, allowableDigChars, false, out message)) return false;

            if (isRKC)
                numAccount = "0" + bicAccount.Substring(4, 2) + numAccount;
            else
                numAccount = bicAccount.Substring(6, 3) + numAccount;

            int[] weight = new int[] { 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1 };

            int sum = 0;
            for (int i = 0; i < weight.Length; i++)
                sum += (int.Parse(numAccount.Substring(i, 1)) * weight[i]) % 10;

            return sum % 10 == 0;
        }
        /*
         * public static bool CheckKeyAccount(string bicAccount, string numAccount, bool isRKC) {
            if (string.IsNullOrEmpty(bicAccount) || string.IsNullOrEmpty(numAccount)) return false;
            if (bicAccount.Length != 9 || numAccount.Length != 20) return false;

            int key = 0;
            if (!int.TryParse(numAccount.Substring(8, 1), out key)) return false;

            char[] chars = numAccount.Replace('A', '0').ToCharArray();
            numAccount = string.Format("{0}{1}0{2}"
                , (isRKC ? string.Format("0{0}", bicAccount.Substring(4, 2)) : bicAccount.Substring(6, 3))
                , numAccount.Substring(0, 8).Replace('A', '0')
                , numAccount.Substring(9));

            if (!CheckChars(numAccount, allowableDigChars, false)) return false;

            int[] weight = new int[] { 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1, 3, 7, 1 };

            int sum = 0;
            for (int i = 0; i < 23; i++)
                sum += int.Parse(numAccount.Substring(i, 1)) * weight[i];

            numAccount = sum.ToString();
            sum = int.Parse(numAccount.Substring(numAccount.Length - 1)) * 3;
            numAccount = sum.ToString();
            sum = int.Parse(numAccount.Substring(numAccount.Length - 1));

            return (bool)(key == sum);
        }
        */
        /// <summary>
        /// Возвращает признак документа внутренний или внешний, а также признак единого центра прибыли
        /// </summary>
        /// <param name="isLocate">Признак документа внешний или внутренний(true - внутренний, false - внешний)</param>
        /// <param name="isSingleProfitCenter">Признак единого центра прибыли</param>
        /// <exception cref="ArgumentException">БИК банка плательщика получателя пусты, номер счета получателя не задан</exception>
        /// <exception cref="InvalidOperationException">Значение в установке 'Операционный день -> Режим идентификации счета' содержит недопустимое значение</exception>
        public void LocateInfo(out bool isLocate, out bool isSingleProfitCenter) {
            isLocate = isSingleProfitCenter = false;

            string accountR_CR = string.IsNullOrEmpty(this.document.Account_R) ? this.document.Account_CR : this.document.Account_R;

            isLocate = IsLocate(settingBicBank, this.document.BicExtBank, accountR_CR);
            if (isLocate) {
                this.connection.ClearParameters();
                this.connection.CmdText =
                    "select udb.NUM_DIVISION_GROUP" +
                    " from OD_ACCOUNTS0 adb, OD_ACCOUNTS0 acr, UBS_DIVISION udb, UBS_DIVISION ucr" +
                    " where  adb.NUM_DIVISION = udb.NUM_DIVISION and acr.NUM_DIVISION = ucr.NUM_DIVISION" +
                        " and udb.NUM_DIVISION_GROUP = ucr.NUM_DIVISION_GROUP" +
                        " and adb.STRACCOUNT = @STRACCOUNT_DB and acr.STRACCOUNT = @STRACCOUNT_CR";
                this.connection.AddInputParameter("STRACCOUNT_DB", this.document.Account_DB);
                this.connection.AddInputParameter("STRACCOUNT_CR", accountR_CR);
                isSingleProfitCenter = (this.connection.ExecuteScalar() != null);
            }
        }
        /// <summary>
        /// Проверка номера документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        /// <exception cref="InvalidOperationException">БИК внешнего банка не установлен объекту документ</exception>
        public bool CheckNumberDoc(out string message) {
            message = null;

            if ("№".Equals(this.document.Number, StringComparison.OrdinalIgnoreCase) ||
               "#".Equals(this.document.Number, StringComparison.OrdinalIgnoreCase)) return true;

            // Исправительный документ
            if (this.settingIsNumerateIspr && this.document.KindDoc == 9 && this.document.TypeDoc == 100) {
                if (!System.Text.RegularExpressions.Regex.IsMatch(this.document.Number, "^И-\\d+$")) {
                    message = "Номер исправительного документа не соответствует формату <И-число>";
                    return false;
                }
                return true;
            }


            if (!CheckChars(this.document.Number, allowableDigChars, false, out message)) {
                message = "Номер документа должен содержать цифры или '#' или '№'" + message;
                return false;
            }

            if (settingIsCheckNumberDoc && document.PayLocate != 0) {
                 if (this.document.Number.Length > 6) {
                    message = "Номер документа не может быть больше 6 символов";
                    return false;
                 }

                /*if (IsInnterRegionPayment(settingBicBank)) {
                    if (this.document.Number.EndsWith("000", StringComparison.OrdinalIgnoreCase)) {
                        message = "Номер документа не может иметь трех завершающих нулей";
                        return false;
                    }

                    if (settingIsCheckLengthNumberDoc && this.document.Number.Length > 6 && this.document.TypeSend == 0 ) { // Вид отправки Электронно
                        if (string.IsNullOrEmpty(this.document.BicExtBank) || this.document.BicExtBank.Length != 9) {
                            message = "БИК внешнего банка в документе не установлен либо задан неверно";
                            return false;
                        }

                        if (!IsInnterRegionPayment(this.document.BicExtBank)) {
                            message = "Номер документа для межрегиональных платежей не может быть больше 6 символов";
                            return false;
                        }
                    }
                }*/
            }

            return true;
        }
        /// <summary>
        /// Проверка даты документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckDateDoc(out string message) {
            return CheckDocumentDate(this.document.KindDoc, this.document.DateDoc, out message);
        }

        /// <summary>
        /// Проверка даты документа
        /// </summary>
        /// <param name="documentDate">Дата документа</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        [Obsolete("Следует использовать перегрузку bool CheckDocumentDate(byte kindDoc, DateTime documentDate, out string message)", true)]
        public bool CheckDocumentDate(DateTime documentDate, out string message) {
            return CheckDocumentDate(0, documentDate, out message);
        }

        /// <summary>
        /// Проверка даты документа
        /// </summary>
        /// <param name="kindDoc">Шифр документа ЦБ</param>
        /// <param name="documentDate">Дата документа</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckDocumentDate(byte kindDoc, DateTime documentDate, out string message) {
            message = null;

            if (this.document.KindDoc == 9 || this.document.KindDoc == 17) { // Для мемориальных ордеров
                if (this.document.DateDoc == dt22220101) return false;
                return true;
            }

            if (this.settingDateOD.Subtract(documentDate).TotalDays > 10) {
                message = string.Format("Дата документа {0} должна быть не более чем на 10 дней меньше даты текущего операционного дня {1}", documentDate.ToString("dd.MM.yyyy"), this.settingDateOD.ToString("dd.MM.yyyy"));
                return false;
            }
            return true;
        }


        /// <summary>
        /// Проверка очередности платежа документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckOcherPaymDoc(out string message) {
            return UbsODCheckDocument.CheckPaymentPriority(this.connection, this.ubs, this.document.PriorityPay, out message);
        }

        /// <summary>
        /// Проверка очередности платежа документа
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с СП</param>
        /// <param name="paymentPriority">Очередность платежа</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public static bool CheckPaymentPriority(IUbsDbConnection connection, IUbsWss ubs, byte paymentPriority, out string message) {
            message = null;
            if (paymentPriority < 1 || paymentPriority > 5) {
                message = "Очередность платежа должна быть в диапазоне от 1 до 5";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Проверка вида документа на платежное требование/платежное поручение
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckKindDoc(out string message) {
            return CheckKindDoc(this.document.KindDoc, this.document.RefSource, out message);
        }

        /// <summary>
        /// Проверка вида документа на диапазон 1..11 и 16,17
        /// </summary>
        /// <param name="kindDoc">Шифр документа ЦБ</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        //[Obsolete("Следует использовать перегрузку bool CheckKindDoc(byte kindDoc, string refSource, out string message)", true)]
        public static bool CheckKindDoc(byte kindDoc, out string message) {
            message = null;
            if ((kindDoc < 1 || kindDoc > 11) && kindDoc != 16 && kindDoc != 17) {
                message = "Шифр документа должен быть в диапазоне от 1 до 11 или 16 или 17";
                return false;
            }
            return true;
        }


        /// <summary>
        /// Проверка вида документа
        /// </summary>
        /// <param name="kindDoc">Шифр документа ЦБ</param>
        /// <param name="refSource">Источник поступления</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckKindDoc(byte kindDoc, string refSource, out string message) {
            message = null;
            if ((kindDoc < 1 || kindDoc > 11) && kindDoc != 16 && kindDoc != 17) {
                message = "Шифр документа должен быть в диапазоне от 1 до 11 или 16 или 17";
                return false;
            }
            
            // Только для ДБО
            //if ("Интернет-Клиент".Equals(refSource, StringComparison.OrdinalIgnoreCase)) {
            //    //if (kindDoc < 1 || kindDoc > 2) {
            //    //    message = "Для документа \"Интернет-клиент\" вид документа должен быть в диапазоне от 1 до 2.";
            //    //    return false;
            //    //}
            //    if (kindDoc != 1 && this.settingСodesOfTheRegions.Contains(this.settingBicBank.Substring(2, 2))) {
            //        message = "Для межрегиональных платежей вид операции д.б. 'Платежное поручение'";
            //        return false;
            //    }
            //}

            return true;
        }

        /// <summary>
        /// Проверка вида отправки документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckTypeSendDoc(out string message) {
            message = null;

            if (this.document.TypeSend != 0 && this.document.TypeSend != 1 && 
                this.document.TypeSend != 2 && this.document.TypeSend != 3 &&
                this.document.TypeSend != 100) {
                message = "Вид отправки документа, должен быть: электронно(0), почтой(1), телеграфом(2), срочно(3)";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Проверка суммы документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckSumDoc(out string message) {
            message = null;

            if (this.document.SummaDB < 0) {
                message = string.Format("Сумма документа по дебету <{0}> указана неверно", this.document.SummaDB.ToString("C", formatCurrency2));
                return false;
            }

            if (this.document.SummaCR < 0) {
                message = string.Format("Сумма документа по кредиту <{0}> указана неверно", this.document.SummaCR.ToString("C", formatCurrency2));
                return false;
            }

            if (this.document.SummaDB == 0 && this.document.SummaCR == 0 && this.document.DateDoc != dt19900101 /*не шаблон*/) {
                message = "Сумма документа не задана";
                return false;
            }

            return true;
        }



        /// <summary>
        /// Проверка счета плательщика
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckNumAccountPDoc(out string message) {
            return CheckAccountPayer(this.document.Account_P, this.document.KindDoc, this.document.DateTrn, out message);
        }

        /// <summary>
        /// Проверка счета плательщика
        /// </summary>
        /// <param name="payerAccount">Счет плательщика</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        [Obsolete("Следует использовать перегрузку метода bool CheckAccountPayer(string payerAccount, byte kindDoc, DateTime transactionDate, out string message)", true)]
        public bool CheckAccountPayer(string payerAccount, out string message) {
            return CheckAccountPayer(payerAccount, 0, dt22220101, out message);
        }

        /// <summary>
        /// Проверка счета плательщика
        /// </summary>
        /// <param name="payerAccount">Счет плательщика</param>
        /// <param name="kindDoc">Шифр документа ЦБ</param>
        /// <param name="transactionDate">Дата операции</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckAccountPayer(string payerAccount, byte kindDoc, DateTime transactionDate, out string message) {
            return CheckAccountPayer(payerAccount, kindDoc, transactionDate, false, out message);
        }

        /// <summary>
        /// Проверка счета плательщика
        /// </summary>
        /// <param name="payerAccount">Счет плательщика</param>
        /// <param name="kindDoc">Шифр документа ЦБ</param>
        /// <param name="transactionDate">Дата операции</param>
        /// <param name="ignoreState">Игнорировать состояние счета кроме Закрыт(1)</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckAccountPayer(string payerAccount, byte kindDoc, DateTime transactionDate, bool ignoreState, out string message) {
            message = null;

            if (!CheckChars(payerAccount, allowableDigChars, false, out message) || payerAccount.Length != 20) {
                message = "Номер счета плательщика содержит недопустимые символы или длина счета неверна" + message;
                return false;
            }

            //string codeCurrency = payerAccount.Substring(5, 3);
            //if (!"810".Equals(codeCurrency, StringComparison.OrdinalIgnoreCase) &&
            //    !"643".Equals(codeCurrency, StringComparison.OrdinalIgnoreCase)) {
            //    message = "Код валюты в номере счета плательщика не рубли";
            //    return false;
            //}

            if (!CheckKeyAccount(this.settingBicBank, payerAccount)) {
                message = "Номер счета плательщика: неверный ключ счета";
                return false;
            }

            if (!CheckAccountStateAndAuthority(payerAccount, kindDoc, transactionDate, false, ignoreState, true, out message)) return false;

            return true;
        }

        /// <summary>
        /// Проверка БИК-а получателя документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckBicRDoc(out string message) {
            message = null;

            if (string.IsNullOrEmpty(this.document.BicExtBank)) {
                if (this.document.PayLocate == 0) return true;
                message = "БИК банка получателя не указан";
                return false;
            }
            // Ищем банк с указанным БИКом
            this.connection.ClearParameters();
            this.connection.CmdText = "select FLAGS from COM_DIC_BANK where BIC = @BIC";
            this.connection.AddInputParameter("BIC", this.document.BicExtBank);
            object value = this.connection.ExecuteScalar();
            if (value == null) {
                message = string.Format("Банк с БИК-ом '{0}' в словаре банков не найден", this.document.BicExtBank);
                return false;
            }
            if ((byte)value == 0) {
                message = string.Format("Банк с БИК-ом '{0}' удален", this.document.BicExtBank);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Проверка БИКа на опасные типы кредитных организаций
        /// </summary>
        /// <param name="connection">Соединение с БД</param>
        /// <param name="bic">Бик получателя</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public static bool CheckBicDangerous(IUbsDbConnection connection, string bic, out string message) {
            message = null;

            // Ищем банк с указанным БИКом
            connection.ClearParameters();
            connection.CmdText =
                "select t.SHORT_NAME  from COM_DIC_BANK b, COM_DIC_TYPE_BANK t" +
                " where b.ID_TYPE_BANK = t.ID_TYPE and b.BIC = @BIC";
            connection.AddInputParameter("BIC", bic);
            object value = connection.ExecuteScalar();
            if (value != null && value != DBNull.Value)
                if ("НКО".Equals((string)value, StringComparison.OrdinalIgnoreCase) ||
                   "ФНКО".Equals((string)value, StringComparison.OrdinalIgnoreCase) ||
                   "НДКО".Equals((string)value, StringComparison.OrdinalIgnoreCase) ||
                   "ФНДКО".Equals((string)value, StringComparison.OrdinalIgnoreCase)) {
                    message = string.Format("Банк с БИК-ом '{0}' относится к организации типа {1}", bic, value);
                    return false;
                }
            return true;
        }

        /// <summary>
        /// Проверка корсчета банка получателя документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckNumCorrAccountRDoc(out string message) {
            message = null;

            if (string.IsNullOrEmpty(this.document.AccountExtBank) ||
                "00000000000000000000".Equals(this.document.AccountExtBank, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (string.IsNullOrEmpty(this.document.BicExtBank)) {
                if (this.document.PayLocate == 0) return true;
                message = "БИК банка получателя не указан";
                return false;
            }

            // Ищем банк с указанным БИКом
            this.connection.ClearParameters();
            this.connection.CmdText = "select CORR_ACC from COM_DIC_BANK where BIC = @BIC";
            this.connection.AddInputParameter("BIC", this.document.BicExtBank);
            object value = this.connection.ExecuteScalar();
            if (value == null) {
                message = string.Format("Банк с БИК-ом '{0}' в словаре банков не найден", this.document.BicExtBank);
                return false;
            }
            if (!this.document.AccountExtBank.Equals((string)value, StringComparison.OrdinalIgnoreCase)) {
                message = "Коррсчет получателя не соответствует коррсчету из справочника банков";
                return false;
            }

            return true;
        }
        /// <summary>
        /// Проверка наименования банка получателя документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckBankNameRDoc(out string message) {
            return CheckBankNameR(this.document.BicExtBank, this.document.NameExtBank, out message);
        }
        /// <summary>
        /// Проверка наименования банка получателя
        /// </summary>
        /// <param name="bicExtBank">БИК внешнего банка</param>
        /// <param name="nameExtBank">Наименование внешнего банка</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckBankNameR(string bicExtBank, string nameExtBank, out string message) {
            message = null;

            if (string.IsNullOrEmpty(bicExtBank)) {
                if (this.document.PayLocate == 0) return true;
                message = "БИК банка получателя не указан";
                return false;
            }

            if (!CheckChars(nameExtBank, allowablePaymentChars, false, out message)) {
                message = "Наименование банка получателя содержит недопустимые символы либо наименование не задано" + message;
                return false;
            }

            // Ищем банк с указанным БИКом
            this.connection.ClearParameters();
            this.connection.CmdText = "select FULL_NAME from COM_DIC_BANK where BIC = @BIC";
            this.connection.AddInputParameter("BIC", bicExtBank);
            object value = this.connection.ExecuteScalar();
            if (value == null) {
                message = string.Format("Банк с БИК-ом '{0}' в словаре банков не найден", bicExtBank);
                return false;
            }
            if (!Convert.ToString(value).Equals(nameExtBank, StringComparison.OrdinalIgnoreCase)) {
                message = string.Format("Наименование банка получателя по справочнику банков '{0}' не совпадает c наименованием в документе '{1}'", value, nameExtBank);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Проверка реквизитов банка
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с СП</param>
        /// <param name="bic">Бик банка</param>
        /// <param name="name">Наименование банка</param>
        /// <param name="corrAccount">Номер корр. счета банка</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена, false - проверка не пройдена</returns>
        public static bool CheckBankDetails(IUbsDbConnection connection, IUbsWss ubs, string bic, string name, string corrAccount, out string message) {
            return CheckBankDetails(connection, ubs, bic, name, corrAccount, true, out message);
        }

        /// <summary>
        /// Проверка реквизитов банка
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с СП</param>
        /// <param name="bic">Бик банка</param>
        /// <param name="corrAccount">Номер корр. счета банка</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена, false - проверка не пройдена</returns>
        public static bool CheckBankDetails(IUbsDbConnection connection, IUbsWss ubs, string bic, string corrAccount, out string message) {
            return CheckBankDetails(connection, ubs, bic, null, corrAccount, false, out message);
        }


        /// <summary>
        /// Проверка реквизитов банка
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с СП</param>
        /// <param name="bic">Бик банка</param>
        /// <param name="name">Наименование банка</param>
        /// <param name="corrAccount">Номер корр. счета банка</param>
        /// <param name="chekName">Признак проверки наименования банка</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена, false - проверка не пройдена</returns>
        public static bool CheckBankDetails(IUbsDbConnection connection, IUbsWss ubs, string bic, string name, string corrAccount, bool chekName, out string message) {
            message = null;

            if (string.IsNullOrEmpty(bic)) {
                message = "БИК банка не указан";
                return false;
            }

            // Ищем банк с указанным БИКом
            connection.ClearParameters();
            connection.CmdText = "select FULL_NAME, SHORT_NAME, CORR_ACC, FLAGS from COM_DIC_BANK where BIC = '" + bic + "'";

            object[] values = connection.ExecuteReadFirstRec();
            if (values == null) {
                message = string.Format("Банк с БИК-ом '{0}' в словаре банков не найден", bic);
                return false;
            }

            if (chekName) {
                if (!CheckChars(name, allowablePaymentChars, false, out message)) {
                    message = "Наименование банка содержит недопустимые символы либо наименование не задано";
                    return false;
                }

                if (!(Convert.ToString(values[0]).Equals(name, StringComparison.OrdinalIgnoreCase) ||
                      Convert.ToString(values[1]).Equals(name, StringComparison.OrdinalIgnoreCase))) {
                    message = string.Format("Наименование банка по справочнику банков <{0}> не соответствует проверяемому наименованию <{1}>", values[1], name);
                    return false;
                }
            }
            else if (!CheckChars(name, allowablePaymentChars, true, out message)) {
                message = "Наименование банка содержит недопустимые символы" + message;
                return false;
            }

            if (string.IsNullOrEmpty(corrAccount)) {
                message = "Коррсчет банка не задан";
                return false;
            }

            if (!Convert.ToString(values[2]).Equals(corrAccount, StringComparison.OrdinalIgnoreCase)) {
                message = string.Format("Коррсчет банка по справочнику банков <{0}> не соответствует проверяемому коррсчету <{1}>", values[2], corrAccount);
                return false;
            }


            if (Convert.ToByte(values[3]) == 0) {
                message = string.Format("Банк с БИК-ом <{0}> удален", bic);
                return false;
            }

            return true;
            //return CheckBicDangerous(connection, bic, out message);;
        }

        /// <summary>
        /// Проверка счета получателя документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        /// <exception cref="ArgumentException">БИК счета ДБ или КР пусты, номер счета КР не задан</exception>
        /// <exception cref="InvalidOperationException">Значение в установке 'Операционный день -> Режим идентификации счета' содержит недопустимое значение</exception>
        public bool CheckNumAccountRDoc(out string message) {
            return CheckAccountRecipient(this.document.PayLocate, this.document.Account_CR, this.document.Account_R, this.document.BicExtBank, this.document.KindDoc, this.document.DateTrn, out message);
        }
        /// <summary>
        /// Проверка счета получателя
        /// </summary>
        /// <param name="accountR">Номер счета получателя</param>
        /// <param name="bicExtBank">БИК внешнего банка</param>
        /// <param name="accountExtBankNotUsed">Коррсчет внешнего банка</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        /// <exception cref="ArgumentException">БИК счета ДБ или КР пусты, номер счета КР не задан</exception>
        /// <exception cref="InvalidOperationException">Значение в установке 'Операционный день -> Режим идентификации счета' содержит недопустимое значение</exception>
        [Obsolete("Следует использовать перегрузку bool CheckAccountRecipient(byte payLocate, string creditAccount, string recipientAccount, string bicExteranalBank, byte kindDoc, DateTime transactionDate, out string message)", true)]
        public bool CheckNumAccountR(string accountR, string bicExtBank, string accountExtBankNotUsed, out string message) {
            message = null;

            if (!CheckChars(accountR, allowableDigChars, false, out message) || accountR.Length != 20) {
                message = "Номер счета получателя содержит недопустимые символы или длина счета неверна" + message;
                return false;
            }

            //string codeCurrency = accountR.Substring(5, 3);
            //if (!"810".Equals(codeCurrency, StringComparison.OrdinalIgnoreCase) &&
            //    !"643".Equals(codeCurrency, StringComparison.OrdinalIgnoreCase)) {
            //    message = "Код валюты в номере счета получателя не рубли";
            //    return false;
            //}

            if (!CheckKeyAccount(bicExtBank, accountR)) {
                message = "Номер счета получателя: неверный ключ счета";
                return false;
            }

            if (IsLocate(settingBicBank, bicExtBank, accountR)) {
                this.connection.ClearParameters();
                this.connection.CmdText =
                    "select a.STATE from OD_ACCOUNTS0 a where a.STRACCOUNT = @STRACCOUNT";
                this.connection.AddInputParameter("STRACCOUNT", accountR);
                object value = this.connection.ExecuteScalar();

                if (value != null) { // Счета в филиальной базе может не быть.
                    // Открыт(0) Закрыт(1) Заблокирован(2) Заморожен(3) Зарезервирован(4)
                    byte state = (byte)value;
                    if (state == 1) {
                        message = string.Format("Cчет <{0}> закрыт", accountR);
                        return false;
                    }
                    // 0 -  сохранение невозможно, 1 – сохранение возможно, 2 – сохранение возможно если счет заблокирован, 3 сохранение возможно, если счет заморожен
                    else if (state == 2) {
                        if (settingSaveAccountBlockFreaze != 1 && settingSaveAccountBlockFreaze != 2) {
                            message = string.Format("Cчет <{0}> заблокирован", accountR);
                            return false;
                        }
                    }
                    else if (state == 3) {
                        if (settingSaveAccountBlockFreaze != 1 && settingSaveAccountBlockFreaze != 3) {
                            message = string.Format("Cчет <{0}> заморожен", accountR);
                            return false;
                        }
                    }
                    else if (state == 4) {
                        message = string.Format("Cчет <{0}> зарезервирован", accountR);
                        return false;
                    }
                    else if (state != 0) {
                        message = string.Format("Состояние '{0}' счета '{1}' не известно", state, accountR);
                        return false;
                    }
                }
            }
            else {
                this.connection.ClearParameters();
                this.connection.CmdText =
                    "select STRACCOUNT from COM_DIC_ACC_CLOSE where BIC = @BIC and STRACCOUNT = @STRACCOUNT";
                this.connection.AddInputParameter("BIC", bicExtBank);
                this.connection.AddInputParameter("STRACCOUNT", accountR);
                if (this.connection.ExecuteScalar() != null) {
                    message = string.Format("Номер счета '{0}' найден в списке закрытых счетов", accountR);
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Проверка счета получателя
        /// </summary>
        /// <param name="payLocate">Направление документа</param>
        /// <param name="creditAccount">Счет кредит</param>
        /// <param name="recipientAccount">Счет получателя</param>
        /// <param name="bicExteranalBank">БИК внешнего банка</param>
        /// <param name="accountExternalBank">Корр. счет внешнего банка</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        [Obsolete("Следует использовать перегрузку bool CheckAccountRecipient(byte payLocate, string creditAccount, string recipientAccount, string bicExteranalBank, byte kindDoc, DateTime transactionDate, out string message)", true)]
        public bool CheckAccountRecipient(byte payLocate, string creditAccount, string recipientAccount, string bicExteranalBank, string accountExternalBank, out string message) {
            return CheckAccountRecipient(payLocate, creditAccount, recipientAccount, bicExteranalBank, 0, dt22220101, out message);
        }

        /// <summary>
        /// Проверка счета получателя
        /// </summary>
        /// <param name="payLocate">Направление документа</param>
        /// <param name="creditAccount">Счет кредит</param>
        /// <param name="recipientAccount">Счет получателя</param>
        /// <param name="bicExteranalBank">БИК внешнего банка</param>
        /// <param name="kindDoc">Шифр документа ЦБ</param>
        /// <param name="transactionDate">Дата операции</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckAccountRecipient(byte payLocate, string creditAccount, string recipientAccount, string bicExteranalBank, byte kindDoc, DateTime transactionDate, out string message) {
            return CheckAccountRecipient(payLocate, creditAccount, recipientAccount, bicExteranalBank, kindDoc, transactionDate, false, out message);
        }

        /// <summary>
        /// Проверка счета получателя
        /// </summary>
        /// <param name="payLocate">Направление документа</param>
        /// <param name="creditAccount">Счет кредит</param>
        /// <param name="recipientAccount">Счет получателя</param>
        /// <param name="bicExteranalBank">БИК внешнего банка</param>
        /// <param name="kindDoc">Шифр документа ЦБ</param>
        /// <param name="transactionDate">Дата операции</param>
        /// <param name="ignoreState">Игнорировать состояние счета кроме Закрыт(1)</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckAccountRecipient(byte payLocate, string creditAccount, string recipientAccount, string bicExteranalBank, byte kindDoc, DateTime transactionDate, bool ignoreState, out string message) {
            message = null;

            if (payLocate == 0) {
                if (string.IsNullOrEmpty(creditAccount) || "00000000000000000000".Equals(creditAccount)) {
                    message = "Номер счета получателя (КР) не задан";
                    return false;
                }

                if (!CheckChars(creditAccount, allowableDigChars, false, out message) || creditAccount.Length != 20) {
                    message = string.Format("Номер счета получателя (КР) <{0}> содержит недопустимые символы или длина счета неверна", creditAccount) + message;
                    return false;
                }

                if (!CheckKeyAccount(this.settingBicBank, creditAccount)) {
                    message = string.Format("Номер счета получателя (КР) <{0}> неверный ключ счета", creditAccount);
                    return false;
                }

                if (!string.IsNullOrEmpty(recipientAccount) && !"00000000000000000000".Equals(recipientAccount)) {
                    if (!CheckKeyAccount(this.settingBicBank, recipientAccount)) {
                        message = string.Format("Номер счета получателя (П) <{0}> неверный ключ счета", recipientAccount);
                        return false;
                    }
                }

                if (!CheckAccountStateAndAuthority(creditAccount, kindDoc, transactionDate, true, ignoreState, false, out message)) return false;

                return true;
            }

            // Внешня платежка может быть без счета
            if (string.IsNullOrEmpty(recipientAccount) || "00000000000000000000".Equals(recipientAccount)) return true;

            /////////////////////////////////////////////////////////////////////////////////////////////
            //return CheckNumAccountR(recipientAccount, bicExteranalBank, accountExternalBank, out message);
            /////////////////////////////////////////////////////////////////////////////////////////////


            if (!CheckChars(recipientAccount, allowableDigChars, false, out message) || recipientAccount.Length != 20) {
                message = string.Format("Номер счета получателя (П) <{0}> содержит недопустимые символы или длина счета неверна", recipientAccount) + message;
                return false;
            }

            //string codeCurrency = recipientAccount.Substring(5, 3);
            //if (!"810".Equals(codeCurrency, StringComparison.OrdinalIgnoreCase) &&
            //    !"643".Equals(codeCurrency, StringComparison.OrdinalIgnoreCase)) {
            //    message = "Код валюты в номере счета получателя не рубли";
            //    return false;
            //}

            if (!CheckKeyAccount(bicExteranalBank, recipientAccount)) {
                message = string.Format("Номер счета получателя (П) <{0}> неверный ключ счета", recipientAccount);
                return false;
            }

            if (this.settingBicBank.Equals(bicExteranalBank, StringComparison.OrdinalIgnoreCase)) {
                if (!CheckAccountStateAndAuthority(recipientAccount, kindDoc, transactionDate, true, ignoreState, false, out message)) return false;
            }

            if (payLocate == 2) {
                if (SearchAccountInCloseList(this.connection, this.ubs, bicExteranalBank, recipientAccount) != null) {
                    message = string.Format("Номер счета <{0}> найден в списке закрытых счетов и правоприемников", recipientAccount);
                    return false;
                }
            }

            return true;
        }

        private bool CheckAccountStateAndAuthority(string straccount, byte kindDoc, DateTime transactionDate, bool missing, bool ignoreState, bool checkAuthorities, out string message) {
            message = null;

            this.connection.ClearParameters();
            this.connection.CmdText =
                "select a.STATE, isnull(END_AUTHORITIES, " + this.connection.sqlDate(dt22220101) + ") from OD_ACCOUNTS0 a where a.STRACCOUNT = '" + straccount + "'";
            object[] record = this.connection.ExecuteReadFirstRec();

            // Счета в филиальной базе может не быть.
            if (record == null) {
                if (missing) return true;
                message = string.Format("Номер счета <{0}> не найден", straccount);
                return false;
            }

            // Открыт(0) Закрыт(1) Заблокирован(2) Заморожен(3) Зарезервирован(4)
            byte state = Convert.ToByte(record[0]);
            DateTime endAuthorities = Convert.ToDateTime(record[1]);

            if (state == 1) {
                message = string.Format("Cчет <{0}> закрыт", straccount);
                return false;
            }

            if (!ignoreState) {
                if (state == 2 && settingSaveAccountBlockFreaze != 1 && settingSaveAccountBlockFreaze != 2) {
                    message = string.Format("Cчет <{0}> заблокирован", straccount);
                    return false;
                }
                else if (state == 3 && settingSaveAccountBlockFreaze != 1 && settingSaveAccountBlockFreaze != 3) {
                    message = string.Format("Cчет <{0}> заморожен", straccount);
                    return false;
                }
                else if (state == 4) {
                    message = string.Format("Cчет <{0}> зарезервирован", straccount);
                    return false;
                }
                else if (state != 0) {
                    message = string.Format("Состояние <{0}> счета <{1}> не известно", state, straccount);
                    return false;
                }
            }

            // Платежное поручение
            if (kindDoc == 1 && endAuthorities != dt22220101 && checkAuthorities) {
                if (endAuthorities <= transactionDate) {
                    message = string.Format("Срок полномочий по счету <{0}> истек <{1}>", straccount, endAuthorities.ToString("dd.MM.yyyy"));
                    return false;
                }
            }

            return true;
        }


        private static bool CheckInn(IUbsDbConnection connection, string inn, string straccount, byte razdel, string settingInnBank, out string message) {
            message = null;
            if ("0".Equals(inn, StringComparison.OrdinalIgnoreCase)) return true;

            //bool flag = false;
            //for (int i = 0; i <= settingInnAllowableLen.GetUpperBound(1); i++)
            //    if (inn.Length >= settingInnAllowableLen[0, i] && inn.Length <= settingInnAllowableLen[1, i]) { flag = true; break; }
            //if (!flag) {
            //    message = string.Format("ИНН <{0}> не проходит по установке <Операционный день/ИНН - допустимая длина>", inn);
            //    return false;
            //}

            bool canEmptyInn = false;


            if (!string.IsNullOrEmpty(straccount)) {

                connection.ClearParameters();
                connection.CmdText =
                    "select c.KIND_CLIENT, c.INN, c.LONG_NAME from OD_ACCOUNTS" + razdel + " a, CLIENTS c where a.ID_CLIENT > 0 and a.ID_CLIENT = c.ID_CLIENT and a.STRACCOUNT = '" + straccount + "'";
                object[] record = connection.ExecuteReadFirstRec();

                if (record != null) {
                    byte typeClient = Convert.ToByte(record[0]);
                    string innClient = Convert.ToString(record[1]);
                    string nameClient = Convert.ToString(record[2]);

                    // В карточке клиента в поле ИНН может быть указан КИО для нерезидентов юр. лиц (5 символов)

                    if (!string.IsNullOrEmpty(inn) && (inn.Length == 10 || inn.Length == 12 || inn.Length == 5) &&
                        !string.IsNullOrEmpty(innClient) && (innClient.Length == 10 || innClient.Length == 12 || innClient.Length == 5)  &&
                        !(inn.Equals(innClient, StringComparison.OrdinalIgnoreCase) ||
                         inn.Equals(settingInnBank, StringComparison.OrdinalIgnoreCase)
                        )) {
                        message = string.Format("ИНН <{0}> не соответствует ИНН указанному в карточке клиента <{1}> счета <{2}>", inn, nameClient, straccount);
                        return false;
                    }

                    if (!inn.Equals(settingInnBank, StringComparison.OrdinalIgnoreCase)) {
                        if (typeClient == 1) {
                            if (!string.IsNullOrEmpty(inn) && inn.Length != 10 && inn.Length != 5 ) {
                                message = string.Format("ИНН <{0}> юр. лица <{1}> указан неверно", inn, nameClient);
                                return false;
                            }
                        }
                        else {
                            if (!string.IsNullOrEmpty(inn) && inn.Length != 12) {
                                message = string.Format("ИНН <{0}> физ. лица <{0}> указан неверно", inn, nameClient);
                                return false;
                            }
                            canEmptyInn = true;
                        }
                    }
                }
            }

            if (!CheckChars(inn, allowableDigChars, canEmptyInn, out message)) {
                message = string.Format("ИНН <{0}> содержит недопустимые символы либо не задан", inn) + message;
                return false;
            }

            if (!string.IsNullOrEmpty(inn) && !CheckKeyInn(inn)) {
                message = string.Format("ИНН <{0}> указан неверно (проверка контрольного числа в ИНН)", inn);
                return false;
            }

            return true;
        }
        

        /// <summary>
        /// Проверка контрольного числа ИНН
        /// </summary>
        /// <param name="inn">ИНН</param>
        /// <returns>Результат проверки, true - проверка пройдена</returns>
        public static bool CheckKeyInn(string inn) {
            if (inn.Length == 10) {

                if (inn.StartsWith("00", StringComparison.OrdinalIgnoreCase)) return true; // Для Крыма

                int k = 2 * int.Parse(inn.Substring(0, 1)) +
                        4 * int.Parse(inn.Substring(1, 1)) +
                       10 * int.Parse(inn.Substring(2, 1)) +
                        3 * int.Parse(inn.Substring(3, 1)) +
                        5 * int.Parse(inn.Substring(4, 1)) +
                        9 * int.Parse(inn.Substring(5, 1)) +
                        4 * int.Parse(inn.Substring(6, 1)) +
                        6 * int.Parse(inn.Substring(7, 1)) +
                        8 * int.Parse(inn.Substring(8, 1));
                k = (k % 11) % 10;
                return k == int.Parse(inn.Substring(9, 1));
            }
            else if(inn.Length == 12) {

                if (inn.StartsWith("00", StringComparison.OrdinalIgnoreCase)) return true; // Для Крыма

                int k = 7 * int.Parse(inn.Substring(0, 1)) +
                        2 * int.Parse(inn.Substring(1, 1)) +
                        4 * int.Parse(inn.Substring(2, 1)) +
                       10 * int.Parse(inn.Substring(3, 1)) +
                        3 * int.Parse(inn.Substring(4, 1)) +
                        5 * int.Parse(inn.Substring(5, 1)) +
                        9 * int.Parse(inn.Substring(6, 1)) +
                        4 * int.Parse(inn.Substring(7, 1)) +
                        6 * int.Parse(inn.Substring(8, 1)) +
                        8 * int.Parse(inn.Substring(9, 1));
                k = (k % 11) % 10;
                if (k != int.Parse(inn.Substring(10, 1))) return false;

                k = 3 * int.Parse(inn.Substring(0, 1)) +
                    7 * int.Parse(inn.Substring(1, 1)) +
                    2 * int.Parse(inn.Substring(2, 1)) +
                    4 * int.Parse(inn.Substring(3, 1)) +
                   10 * int.Parse(inn.Substring(4, 1)) +
                    3 * int.Parse(inn.Substring(5, 1)) +
                    5 * int.Parse(inn.Substring(6, 1)) +
                    9 * int.Parse(inn.Substring(7, 1)) +
                    4 * int.Parse(inn.Substring(8, 1)) +
                    6 * int.Parse(inn.Substring(9, 1)) +
                    8 * int.Parse(inn.Substring(10, 1));

                k = (k % 11) % 10;
                return k == int.Parse(inn.Substring(11, 1));
            }
            else if (inn.Length == 5) {
                return true; // для КИО проверка не выполняется
            }
            return false;
        }


        /// <summary>
        /// Проверка ИНН плательщика документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckInnPDoc(out string message) {
            string inn = this.document.INN_P;
            string straccount = string.IsNullOrEmpty(this.document.Account_P) ? this.document.Account_P : this.document.Account_DB;

            message = null;
            string field101 = (string)this.document.Field("Статус составителя расчетного документа");
            if (string.IsNullOrEmpty(field101) && string.IsNullOrEmpty(inn)) return true;

            if (this.document.PayLocate == 2) straccount = null; // Для исходящего не проверяем по счету

            bool result = CheckInn(this.connection, inn, straccount, this.document.Razdel, this.settingInnBank, out message);
            if (!result) message = "Плательщик. " + message;
            return result;
        }
        /// <summary>
        /// Проверка ИНН получателя документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckInnRDoc(out string message) {
            string inn = this.document.INN_R;
            string straccount = string.IsNullOrEmpty(this.document.Account_R) ? this.document.Account_R : this.document.Account_CR;

            message = null;
            string field101 = (string)this.document.Field("Статус составителя расчетного документа");
            if (string.IsNullOrEmpty(field101) && string.IsNullOrEmpty(inn)) return true;

            if (this.document.PayLocate == 1) straccount = null; // Для входящего не проверяем по счету

            bool result = CheckInn(this.connection, inn, straccount, this.document.Razdel, this.settingInnBank, out message);
            if (!result) message = "Получатель. " + message;
            return result;
        }

        /// <summary>
        /// Проверка ИНН плательщика раздел А
        /// </summary>
        /// <param name="inn">ИНН плательщика</param>
        /// <param name="straccount">Номер счета плательщика</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckInnP(string inn, string straccount, out string message) {
            bool result = CheckInn(this.connection, inn, straccount, 0, this.settingInnBank, out message);
            if (!result) message = "Плательщик. " + message;
            return result;
        }

        /// <summary>
        /// Проверка ИНН получателя раздел А
        /// </summary>
        /// <param name="inn">ИНН получателя</param>
        /// <param name="straccount">Номер счета получателя</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckInnR(string inn, string straccount, out string message) {
            bool result = CheckInn(this.connection, inn, straccount, 0, this.settingInnBank, out message);
            if (!result) message = "Получатель. " + message;
            return result;
        }

        /// <summary>
        /// Проверка ИНН плательщика
        /// </summary>
        /// <param name="inn">ИНН получателя</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        [Obsolete("Если плательщик находиться в нашем банке, следует использовать перегруженный метод с передачей счета", false)]
        public bool CheckInnP(string inn, out string message) {
            return CheckInnP(inn, null, out message);
        }
        /// <summary>
        /// Проверка ИНН получателя
        /// </summary>
        /// <param name="inn">ИНН получателя</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        [Obsolete("Если плательщик находиться в нашем банке, следует использовать перегруженный метод с передачей счета", false)]
        public bool CheckInnR(string inn, out string message) {
            return CheckInnR(inn, null, out message);
        }

        /// <summary>
        /// Проверка наименования плательщика на длину
        /// </summary>
        /// <param name="name">Наименование плательщика</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckNamePayerLength(string name, out string message) {
            message = null;
            if (!CheckChars(name, allowablePaymentChars, false, out message)) {
                message = "Наименование плательщика содержит недопустимые символы либо наименование не задано" + message;
                return false;
            }

            if (name.Length > 160) {
                message = "Наименование плательщика превышает 160 символов";
                return false;
            }
            return true;
        }
        /// <summary>
        /// Проверка наименования плательщика документа на допустимые символы и соответствие наименованию владельца счета ИНН и КПП
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckNamePayer(out string message) {
            return CheckNamePayer(new MakeNameParameters(this.document), this.document.Name_P, this.document.INN_P, (string)this.document.Field("КПП плательщика"), out message);
        }
        /// <summary>
        /// Проверка наименования плательщика документа на допустимые символы и соответствие наименованию владельца счета и ИНН
        /// </summary>
        /// <param name="p">Параметры платежа</param>
        /// <param name="name">Наименование плательщика</param>
        /// <param name="inn">ИНН плательщика</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        [Obsolete("Следует использовать метод bool CheckNamePayer(MakeNameParameters p, string name, string inn, string kpp, out string message)", true)]
        public bool CheckNamePayer(MakeNameParameters p, string name, string inn, out string message) {
            return CheckNamePayer(p, name, inn, null, true, out message);
            
        }
        /// <summary>
        /// Проверка наименования плательщика документа на допустимые символы и соответствие наименованию владельца счета, ИНН и КПП
        /// </summary>
        /// <param name="p">Параметры платежа</param>
        /// <param name="name">Наименование плательщика</param>
        /// <param name="inn">ИНН плательщика</param>
        /// <param name="kpp">КПП плательщика</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckNamePayer(MakeNameParameters p, string name, string inn, string kpp, out string message) {
            return CheckNamePayer(p, name, inn, kpp, true, out message);
        }
        /// <summary>
        /// Проверка наименования плательщика документа на допустимые символы и соответствие наименованию владельца счета и ИНН
        /// </summary>
        /// <param name="p">Параметры платежа</param>
        /// <param name="name">Наименование плательщика</param>
        /// <param name="inn">ИНН плательщика</param>
        /// <param name="replaceAbbreviation">Заменять аббревиатуры</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        [Obsolete("Следует использовать метод bool CheckNamePayer(MakeNameParameters p, string name, string inn, string kpp, bool replaceAbbreviation, out string message)", true)]
        public bool CheckNamePayer(MakeNameParameters p, string name, string inn, bool replaceAbbreviation, out string message) {
            return CheckNamePayer(p, name, inn, null, replaceAbbreviation, out message);
        }
        /// <summary>
        /// Проверка наименования плательщика документа на допустимые символы и соответствие наименованию владельца счета и ИНН
        /// </summary>
        /// <param name="p">Параметры платежа</param>
        /// <param name="name">Наименование плательщика</param>
        /// <param name="inn">ИНН плательщика</param>
        /// <param name="kpp">КПП плательщика</param>
        /// <param name="replaceAbbreviation">Заменять аббревиатуры</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckNamePayer(MakeNameParameters p, string name, string inn, string kpp, bool replaceAbbreviation, out string message) {
            message = null;

            MakeNameParameters pCheck = p.Clone();
            if (!CheckNamePayerLength(name, out message)) return false;
            if (pCheck.PayLocate == 2) return true;

            string testingName = p.IsRBClientName ? ReplaceTrashString(DelimiterString(name)) : ReplaceTrashString(name, replaceAbbreviation).Split(new string[] { "//" }, StringSplitOptions.RemoveEmptyEntries)[0];

            pCheck.TypeClientName = MakeNameParameters.TypeName.Long;
            bool existAccount = MakeNamePayer(pCheck);
            string checkAccountPayer = pCheck.GetAccountPayer();
            bool isCustomerAccount = IsCustomerAccount(checkAccountPayer);
            bool isEmptyInn = string.IsNullOrEmpty(inn) || "0".Equals(inn, StringComparison.Ordinal) || string.IsNullOrEmpty(pCheck.Inn);
            bool isEmptyKpp = string.IsNullOrEmpty(kpp) || "0".Equals(kpp, StringComparison.Ordinal) || string.IsNullOrEmpty(pCheck.Kpp);
            
            if (isCustomerAccount && existAccount && pCheck.ClientId > 0) {
                string name0 = p.IsRBClientName ? ReplaceTrashString(pCheck.TestingName) : ReplaceTrashString(pCheck.TestingName, replaceAbbreviation);
                pCheck.TypeClientName = MakeNameParameters.TypeName.Reduce; MakeNamePayer(pCheck);
                string name1 = p.IsRBClientName ? ReplaceTrashString(pCheck.TestingName) : ReplaceTrashString(pCheck.TestingName, replaceAbbreviation);

                if (!testingName.Equals(name0, StringComparison.OrdinalIgnoreCase) && !testingName.Equals(name1, StringComparison.OrdinalIgnoreCase)) {
                    message = string.Format("Наименование плательщика <{0}> не соответствует полному наименованию владельца счета <{1}>", name, checkAccountPayer);
                    return false;
                }

                if (!isEmptyInn && !inn.Equals(pCheck.Inn, StringComparison.OrdinalIgnoreCase)) {
                    message = string.Format("ИНН плательщика <{0}> не соответствует ИНН владельца счета <{1}>", inn, checkAccountPayer);
                    return false;
                }

                //if (!isEmptyKpp && !kpp.Equals(pCheck.Kpp, StringComparison.OrdinalIgnoreCase)) {
                //    message = string.Format("КПП плательщика <{0}> не соответствует КПП владельца счета", kpp);
                //    return false;
                //}
            }
            else if (!isCustomerAccount || isCustomerAccount && existAccount && pCheck.ClientId == 0) {
                if (p.PayLocate != 2)
                {
                    #region 16.06.2016 Градинар Р.И. обращение 78859 В платежке могут указать полное наименование филиала
                    /* БЫЛО:
                       if (!testingName.Equals(ReplaceTrashString(this.settingNamePaymentDocument), StringComparison.OrdinalIgnoreCase))
                       
                       СТАЛО:
                       if (!testingName.Equals(ReplaceTrashString(this.settingNamePaymentDocument), StringComparison.OrdinalIgnoreCase)
                           &&
                           !testingName.Equals(ReplaceTrashString(this.settingNameBranchFull), StringComparison.OrdinalIgnoreCase))
                     */
                    if (!testingName.Equals(ReplaceTrashString(this.settingNamePaymentDocument), StringComparison.OrdinalIgnoreCase)
                        &&
                        !testingName.Equals(ReplaceTrashString(this.settingNameBranchFull), StringComparison.OrdinalIgnoreCase)){
                            message = string.Format("Наименование плательщика <{0}> не соответствует наименованию банка", name);
                        return false;
                        }
                    #endregion

                    if (!isEmptyInn && !inn.Equals(this.settingInnBank, StringComparison.Ordinal)) {
                        message = string.Format("ИНН плательщика <{0}> не соответствует ИНН банка", inn);
                        return false;
                    }

                    //if (!isEmptyKpp && !kpp.Equals(this.settingKppBank, StringComparison.Ordinal)) {
                    //    message = string.Format("КПП плательщика <{0}> не соответствует КПП банка", kpp);
                    //    return false;
                    //}
                }
            }

            return true;
        }


        /// <summary>
        /// Проверка наименования получателя на длину и символы
        /// </summary>
        /// <param name="name">Наименование плательщика</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckNameRecipientLength(string name, out string message) {
            message = null;
            if (!CheckChars(name, allowablePaymentChars, false, out message)) {
                message = "Наименование получателя содержит недопустимые символы либо наименование не задано" + message;
                return false;
            }

            if (name.Length > 160) {
                message = "Наименование получателя превышает 160 символов";
                return false;
            }
            return true;
        }
        /// <summary>
        /// Проверка наименования получателя документа на допустимые символы и соответствие наименованию владельца счета
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckNameRecipient(out string message) {
            return CheckNameRecipient(new MakeNameParameters(this.document), this.document.Name_R, this.document.INN_R, (string)this.document.Field("КПП получателя"), out message);
        }
        /// <summary>
        /// Проверка наименования получателя документа на допустимые символы и соответствие наименованию владельца счета и ИНН
        /// </summary>
        /// <param name="p">Параметры платежа</param>
        /// <param name="name">Наименование получателя</param>
        /// <param name="inn">ИНН получателя</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        [Obsolete("Следует использовать метод bool CheckNameRecipient(MakeNameParameters p, string name, string inn, string kpp, out string message)", true)]
        public bool CheckNameRecipient(MakeNameParameters p, string name, string inn, out string message) {
            return CheckNameRecipient(p, name, inn, true, out message);
        }
        /// <summary>
        /// Проверка наименования получателя документа на допустимые символы и соответствие наименованию владельца счета и ИНН
        /// </summary>
        /// <param name="p">Параметры платежа</param>
        /// <param name="name">Наименование получателя</param>
        /// <param name="inn">ИНН получателя</param>
        /// <param name="kpp">КПП получателя</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckNameRecipient(MakeNameParameters p, string name, string inn, string kpp, out string message) {
            return CheckNameRecipient(p, name, inn, kpp, true, out message);
        }
        /// <summary>
        /// Проверка наименования получателя документа на допустимые символы и соответствие наименованию владельца счета и ИНН
        /// </summary>
        /// <param name="p">Параметры платежа</param>
        /// <param name="name">Наименование получателя</param>
        /// <param name="inn">ИНН получателя</param>
        /// <param name="replaceAbbreviation">Заменять аббревиатуры</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        [Obsolete("Следует использовать метод bool CheckNameRecipient(MakeNameParameters p, string name, string inn, string kpp, bool replaceAbbreviation, out string message)", true)]
        public bool CheckNameRecipient(MakeNameParameters p, string name, string inn, bool replaceAbbreviation, out string message) {
            return CheckNameRecipient(p, name, inn, null, replaceAbbreviation, out message);
        }
        /// <summary>
        /// Проверка наименования получателя документа на допустимые символы и соответствие наименованию владельца счета и ИНН
        /// </summary>
        /// <param name="p">Параметры платежа</param>
        /// <param name="name">Наименование получателя</param>
        /// <param name="inn">ИНН получателя</param>
        /// <param name="kpp">КПП получателя</param>
        /// <param name="replaceAbbreviation">Заменять аббревиатуры</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckNameRecipient(MakeNameParameters p, string name, string inn, string kpp, bool replaceAbbreviation, out string message) {
            message = null;

            MakeNameParameters pCheck = p.Clone();
            if (!CheckNameRecipientLength(name, out message)) return false;

            if (this.settingCheckUFK && !string.IsNullOrEmpty(p.KBK) && p.KBK.Length == 20) {
                if (name.IndexOf("УФК", StringComparison.Ordinal) < 0 &&
                    name.Replace(" ", "").IndexOf("УправлениеФедеральногоКазначейства", StringComparison.OrdinalIgnoreCase) < 0) {
                    message = string.Format("Наименование получателя <{0}> для платежей в бюджет и бюджетную систему должно содержать значение <УФК> или <Управление Федерального Казначейства>", name);
                    return false;
                }
            }

            if (pCheck.PayLocate == 1) return true;
            string testingName = p.IsRBClientName ? ReplaceTrashString(DelimiterString(name)) : ReplaceTrashString(name, replaceAbbreviation).Split(new string[] { "//" }, StringSplitOptions.RemoveEmptyEntries)[0];
            
            pCheck.TypeClientName = MakeNameParameters.TypeName.Long;
            bool existAccount = MakeNameRecipient(pCheck);
            string checkAccountRecipient = pCheck.GetAccountRecipient();
            bool isCustomerAccount = IsCustomerAccount(checkAccountRecipient);
            bool isEmptyInn = string.IsNullOrEmpty(inn) || "0".Equals(inn, StringComparison.Ordinal) || string.IsNullOrEmpty(pCheck.Inn);
            bool isEmptyKpp = string.IsNullOrEmpty(kpp) || "0".Equals(kpp, StringComparison.Ordinal) || string.IsNullOrEmpty(pCheck.Kpp);

            if (isCustomerAccount && existAccount && pCheck.ClientId > 0) {
                string name0 = p.IsRBClientName ? ReplaceTrashString(pCheck.TestingName) : ReplaceTrashString(pCheck.TestingName, replaceAbbreviation), name1 = null, name2 = null;

                if (pCheck.Client.Type == 1) {
                    pCheck.TypeClientName = MakeNameParameters.TypeName.Reduce; MakeNameRecipient(pCheck);
                    name1 = p.IsRBClientName ? ReplaceTrashString(pCheck.TestingName) : ReplaceTrashString(pCheck.TestingName, replaceAbbreviation);
                    pCheck.TypeClientName = MakeNameParameters.TypeName.Short; MakeNameRecipient(pCheck);
                    name2 = p.IsRBClientName ? ReplaceTrashString(pCheck.TestingName) : ReplaceTrashString(pCheck.TestingName, replaceAbbreviation);
                }

                if (!testingName.Equals(name0, StringComparison.OrdinalIgnoreCase) 
                    && !testingName.Equals(name1 ?? name0, StringComparison.OrdinalIgnoreCase)
                    && !testingName.Equals(name2 ?? name0, StringComparison.OrdinalIgnoreCase)) {
                    message = string.Format("Наименование получателя <{0}> не соответствует полному наименованию владельца счета <{1}>", name, checkAccountRecipient);
                    return false;
                }

                if (!isEmptyInn && !inn.Equals(pCheck.Inn, StringComparison.OrdinalIgnoreCase)) {
                    message = string.Format("ИНН получателя <{0}> не соответствует ИНН владельца счета <{0}>", inn, checkAccountRecipient);
                    return false;
                }

                //if (!isEmptyKpp && !kpp.Equals(pCheck.Kpp, StringComparison.OrdinalIgnoreCase)) {
                //    message = string.Format("КПП получателя <{0}> не соответствует КПП владельца счета", kpp);
                //    return false;
                //}
            }
            else if (!isCustomerAccount || isCustomerAccount && existAccount && pCheck.ClientId == 0) {
                if (p.PayLocate != 2) {
                    if (!testingName.Equals(ReplaceTrashString(this.settingNamePaymentDocument), StringComparison.OrdinalIgnoreCase)
                        &&
                        !testingName.Equals(ReplaceTrashString(this.settingNameBranchFull), StringComparison.OrdinalIgnoreCase)){
                        message = string.Format("Наименование получателя <{0}> не соответствует наименованию банка", name);
                        return false;
                    }

                    if (!isEmptyInn && !inn.Equals(this.settingInnBank, StringComparison.Ordinal)) {
                        message = string.Format("ИНН получателя <{0}> не соответствует ИНН банка", inn);
                        return false;
                    }

                    //if (!isEmptyKpp && !kpp.Equals(this.settingKppBank, StringComparison.Ordinal)) {
                    //    message = string.Format("КПП получателя <{0}> не соответствует КПП банка", kpp);
                    //    return false;
                    //}
                }
            }
            return true;
        }


        private static string ReplaceTrashString(string value, bool replaceAbbreviation) {

            if (replaceAbbreviation) {
                foreach (string item in new string[] {
                    "\\bИП\\b",
                    "\\bИндивидуальный\\sпредприниматель\\b",
                    "\\bНотариус\\b",
                    "\\bАдвокат\\b",
                    "\\bКФХ\\b",
                    "\\bООО\\b",
                    "\\bОбщество\\sс\\sограниченной\\sответственностью\\b",
                    "\\bОАО\\b",
                    "\\bОткрытое\\sакционерное\\sобщество\\b",
                    "\\bЗАО\\b",
                    "\\bЗакрытое\\sакционерное\\sобщество\\b",
                    "\\bАО\\b",
                    "\\bАкционерное\\sобщество\\b" }
                ) value = Regex.Replace(value, item, " ", RegexOptions.IgnoreCase);
            }

            value = value.Replace("\"", " ").Replace("(", " ").Replace(")", " ").Replace("'", " ");
            value = Regex.Replace(value, "\\s{2,}", " ");
            value = Regex.Replace(value, "[^A-Za-zА-Яа-я0-9]\\s", m => m.Value[0].ToString());
            value = Regex.Replace(value, "\\s[^A-Za-zА-Яа-я0-9]", m => m.Value[1].ToString());
            return value.Trim();
        }
        private string ReplaceTrashString(string value) {
            foreach (KeyValuePair<string, string> item in this.settingAbbreviation) {
                value = Regex.Replace(value, "\\b" + (item.Key).Replace(" ", "\\s") + "\\b", item.Value, RegexOptions.IgnoreCase);
            }
            value = value.Replace("\"", " ").Replace("(", " ").Replace(")", " ").Replace("'", " ");
            value = Regex.Replace(value, "\\s{2,}", " ");
            value = Regex.Replace(value, "[^A-Za-zА-Яа-я0-9]\\s", m => m.Value[0].ToString());
            value = Regex.Replace(value, "\\s[^A-Za-zА-Яа-я0-9]", m => m.Value[1].ToString());
            return value.Trim();
        }
        private string DelimiterString(string value) {
            foreach(string item in this.settingDelimiter) {
                if (string.IsNullOrEmpty(item)) continue;
                value = value.Split(new string[] { item }, StringSplitOptions.RemoveEmptyEntries)[0];
            }
            return value;
        }

            

        


        /// <summary>
        /// Проверка наименования получателя документа на допустимые символы
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        [Obsolete("Метод устарел, следует использовать метод CheckNameRecipient", true)]
        public bool CheckNameRDoc(out string message) {
            message = null;

            if (!CheckChars(this.document.Name_R, allowablePaymentChars, false, out message)) {
                message = "Наименование получателя содержит недопустимые символы либо наименование не задано" + message;
                return false;
            }

            return true;
        }
        /// <summary>
        /// Проверка наименования получателя документа
        /// </summary>
        /// <param name="nameR">Наименование получателя</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        [Obsolete("Следует использовать метод проверки CheckNameRecipient", true)]
        public bool CheckNameR(string nameR, out string message) {
            message = null;

            if (!CheckChars(nameR, allowablePaymentChars, false, out message)) {
                message = "Наименование получателя содержит недопустимые символы либо наименование не задано" + message;
                return false;
            }

            return true;
        }
        /// <summary>
        /// Проверка назначения платежа документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        /// <exception cref="InvalidOperationException">Номер счета ДБ или КР пусты либо указаны неверно</exception>
        public bool CheckNoteDoc(out string message) {
            return CheckNote(this.document.KindDoc, this.document.Description, this.document.PriorityPay
                , this.document.Account_DB, this.document.Account_P, this.document.Account_CR, this.document.Account_R
                , this.document.PayLocate, (string)this.document.Field("Статус составителя расчетного документа"), out message);
        }
        /// <summary>
        /// Проверка назначения платежа документа на наличии/отсутствие КВВО
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        /// <exception cref="InvalidOperationException">Номер счета ДБ или КР пусты либо указаны неверно</exception>
        public bool CheckNoteForForeignExchangeControl(out string message) {
            message = null;

            byte kinddoc = this.document.KindDoc;
            string description = this.document.Description;

            if (kinddoc == 2 && settingNotCheckNotePayPT) // Платежное требование, непроверять
                return true;

            // Если КВВО не требуется (ни в назначении, ни по документу), но при этом КВВО указан в назначении 
            // НЕ выдавать сообщение об ошибке «Назначение платежа не должно содержать данные о виде валютной операции»;
            int type = GetForeignExchangeControl();
            if (type == 0) {
                //if (description.StartsWith("{VO")) {
                //    message = "Назначение платежа не должно содержать данные о виде валютной операции";
                //    return false;
                //}
            }
            else if (type == 1) { // Если КВВО требуется только по документу, но указан в назначении платежа – ошибка не выдается.

            }
            else {
                if (!description.StartsWith("{VO")) {
                    message = "Назначение платежа не содержит данные о виде валютной операции";
                    return false;
                }
                if (!settingСodesOfCCT.Exists(
                    item => {
                        return !string.IsNullOrEmpty(item) && description.StartsWith("{VO" + item);
                    })) {
                    message = "Назначение платежа не содержит данные о виде валютной операции";
                    return false;
                }
            }
                
            return true;
        }
        /// <summary>
        /// Проверка назначения платежа
        /// </summary>
        /// <param name="kinddoc">Вид документа</param>
        /// <param name="description">Наименование платежа</param>
        /// <param name="priorityPay">Очередность платежа</param>
        /// <param name="accountDb">Номер счета ДБ</param>
        /// <param name="accountP">Номер счета плательщика</param>
        /// <param name="accountCr">Номер счета КР</param>
        /// <param name="accountR">Номер счета получателя</param>
        /// <param name="paylocate">Направление документа</param>
        /// <param name="field101">Статус составителя</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        /// <exception cref="InvalidOperationException">Номер счета ДБ или КР пусты либо указаны неверно</exception>
        public bool CheckNote(byte kinddoc, string description, byte priorityPay, string accountDb, string accountP, string accountCr, string accountR, byte paylocate, string field101, out string message) {
            message = null;

            if (kinddoc == 2 && settingNotCheckNotePayPT) // Платежное требование, непроверять
                return true;

            if (!CheckChars(description, allowablePaymentChars, false, out message)) {
                message = "Назначение платежа содержит недопустимые символы либо назначение не задано" + message;
                return false;
            }
            if (description.Length > 210) {
                message = "Назначение платежа превышает 210 символов";
                return false;
            }

            if (!(paylocate == 0 && (kinddoc == 9 || kinddoc == 17))) {

                if (this.settingCheckNote && priorityPay == 5 && string.IsNullOrEmpty(field101)) {

                    string account0 = string.IsNullOrEmpty(accountP) ? accountDb : accountP;
                    string account1 = string.IsNullOrEmpty(accountR) ? accountCr : accountR;

                    int value = 0;
                    bool flag0 = string.IsNullOrEmpty(account0) || account0.Length < 5 || !settingSearchNDSNotIncludeAcc.TryGetValue(account0.Substring(0, 5), out value) || value == 2;
                    bool flag1 = string.IsNullOrEmpty(account1) || account1.Length < 5 || !settingSearchNDSNotIncludeAcc.TryGetValue(account1.Substring(0, 5), out value) || value == 1;
                    
                    // Проверяем если нет исключений хотя бы по одному счету
                    if (flag0 && flag1) {
                        if (string.IsNullOrEmpty(Regex.Match(description, "\\bНДС\\b", RegexOptions.IgnoreCase).Value)) {
                            message = "Назначение платежа не содержит упоминания о НДС";
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Проверка условия оплаты документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckConditionPayDoc(out string message) {
            return CheckPaymentCondition(this.document.KindDoc, this.document.ConditionPay, out message);
        }

        /// <summary>
        /// Проверка основания (условия) оплаты документа
        /// </summary>
        /// <param name="kinddoc">Вид документа (Шифр документа по ЦБ)</param>
        /// <param name="paymentCondition">Основание (условие) оплаты</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public static bool CheckPaymentCondition(byte kinddoc, string paymentCondition,  out string message) {
            message = null;

            if (!CheckChars(paymentCondition, allowablePaymentChars, true, out message))
                message = "Условие оплаты содержит недопустимые символы" + message;

            if (kinddoc == 2) { // Платежное требование

                if (!("1".Equals(paymentCondition, StringComparison.OrdinalIgnoreCase) /*наличие заранее данного акцепта*/ ||
                      "2".Equals(paymentCondition, StringComparison.OrdinalIgnoreCase) /*необходимость получения акцепта плательщика*/)) {

                    message = "Условие оплаты содержит недопустимые символы либо условие оплаты не задано";
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        /// Проверка срока акцепта документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckTermAcceptanceDoc(out string message) {
            message = null;
            if (this.document.KindDoc == 2 && this.document.TermAccept <= 0) {
                message = "Срок акцепта не указан для платежного требования";
                return false;
            }

            return true;
        }
        /// <summary>
        /// Проверка налоговых полей документа
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckTaxFieldsDoc(out string message) {
            string field101 = (string)this.document.Field("Статус составителя расчетного документа");
            string field102 = (string)this.document.Field("КПП плательщика");
            string field103 = (string)this.document.Field("КПП получателя");
            string field104 = (string)this.document.Field("Код бюджетной классификации");
            string field105 = (string)this.document.Field("Код ОКАТО");
            string field106 = (string)this.document.Field("Основание налогового платежа");
            string field107 = (string)this.document.Field("Налоговый период");
            string field108 = (string)this.document.Field("Номер налогового документа");
            string field109 = (string)this.document.Field("Дата налогового документа");
            string field110 = (string)this.document.Field("Тип налогового платежа");
            string field22 = (string)this.document.Field("УИН");
            string uidPayment = (string)this.document.Field("Уникальный идентификатор платежа");
            string field60 = (string)this.document.INN_P;
            string field61 = (string)this.document.INN_R;

            return CheckTaxFields(field101, field102, field103, field104, field105, field106, field107, field108, field109, field110, field22, uidPayment, null, field60, field61
                , this.document.Account_DB, this.document.Account_P, this.document.Account_R, this.document.BicExtBank, false, out message);
        }

        /// <summary>
        /// Проверка налоговых полей
        /// </summary>
        /// <param name="field101">Статус составителя расчетного документа</param>
        /// <param name="field102">КПП плательщика</param>
        /// <param name="field103">КПП получателя</param>
        /// <param name="field104">Код бюджетной классификации</param>
        /// <param name="field105">ОКТМО</param>
        /// <param name="field106">Основание налогового платежа</param>
        /// <param name="field107">Налоговый период</param>
        /// <param name="field108">Номер налогового документа</param>
        /// <param name="field109">Дата налогового документа</param>
        /// <param name="field110">Тип налогового платежа</param>
        /// <param name="accountDb">Номер счета ДБ</param>
        /// <param name="accountP">Номер счета плательщика</param>
        /// <param name="accountR">Номер счета получателя</param>
        /// <param name="bicExtBank">БИК внешнего банка</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        [Obsolete("Данный метод проверки не учитывает налоговое поле УИН, следует использовать перегруженный метод принимающий данный параметр", true)]
        public bool CheckTaxFields(string field101, string field102, string field103, string field104, string field105, string field106, string field107, string field108, string field109, string field110, string accountDb, string accountP, string accountR, string bicExtBank, out string message) {
            return CheckTaxFields(field101, field102, field103, field104, field105, field106, field107, field108, field109, field110, "0", accountDb, accountP, accountR, bicExtBank, out message);
        }

        /// <summary>
        /// Проверка налоговых полей
        /// </summary>
        /// <param name="field101">Статус составителя расчетного документа</param>
        /// <param name="field102">КПП плательщика</param>
        /// <param name="field103">КПП получателя</param>
        /// <param name="field104">Код бюджетной классификации</param>
        /// <param name="field105">ОКТМО</param>
        /// <param name="field106">Основание налогового платежа</param>
        /// <param name="field107">Налоговый период</param>
        /// <param name="field108">Номер налогового документа</param>
        /// <param name="field109">Дата налогового документа</param>
        /// <param name="field110">Тип налогового платежа</param>
        /// <param name="field22">УИН</param>
        /// <param name="accountDb">Номер счета ДБ</param>
        /// <param name="accountP">Номер счета плательщика</param>
        /// <param name="accountR">Номер счета получателя</param>
        /// <param name="bicExtBank">БИК внешнего банка</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        [Obsolete("Данный метод проверки не учитывает налоговое поле Уникальный идентификатор платежа, следует использовать перегруженный метод принимающий данный параметр", true)]
        public bool CheckTaxFields(string field101, string field102, string field103, string field104, string field105, string field106, string field107, string field108, string field109, string field110, string field22, string accountDb, string accountP, string accountR, string bicExtBank, out string message) {
            return CheckTaxFields(field101, field102, field103, field104, field105, field106, field107, field108, field109, field110, field22, null, null, null, null, accountDb, accountP, accountR, bicExtBank, false, out message);
        }
        /// <summary>
        /// Проверка налоговых полей
        /// </summary>
        /// <param name="field101">Статус составителя расчетного документа</param>
        /// <param name="field102">КПП плательщика</param>
        /// <param name="field103">КПП получателя</param>
        /// <param name="field104">Код бюджетной классификации</param>
        /// <param name="field105">ОКТМО</param>
        /// <param name="field106">Основание налогового платежа</param>
        /// <param name="field107">Налоговый период</param>
        /// <param name="field108">Номер налогового документа</param>
        /// <param name="field109">Дата налогового документа</param>
        /// <param name="field110">Тип налогового платежа</param>
        /// <param name="uin">УИН</param>
        /// <param name="uidPayment">Уникальный идентификатор платежа</param>
        /// <param name="uidPayer">Уникальный идентификатор плательщика</param>
        /// <param name="field60">ИНН плательщика</param>
        /// <param name="field61">ИНН получателя</param>
        /// <param name="accountDb">Номер счета ДБ</param>
        /// <param name="accountP">Номер счета плательщика</param>
        /// <param name="accountR">Номер счета получателя</param>
        /// <param name="bicExtBank">БИК внешнего банка</param>
        /// <param name="isReestr">Проверка по реестру</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckTaxFields(string field101, string field102, string field103, string field104, string field105, string field106, string field107, string field108, string field109, string field110, string uin, string uidPayment, string uidPayer, string field60, string field61, string accountDb, string accountP, string accountR, string bicExtBank, bool isReestr, out string message) {
            message = null;

            if (string.IsNullOrEmpty(field101) &&
                !( /*string.IsNullOrEmpty(field102)&& string.IsNullOrEmpty(field103) && */ string.IsNullOrEmpty(field104) &&
                    /*string.IsNullOrEmpty(field105) &&*/ (string.IsNullOrEmpty(field106) || "0".Equals(field106, StringComparison.OrdinalIgnoreCase)) && string.IsNullOrEmpty(field107) &&
                    string.IsNullOrEmpty(field108) && string.IsNullOrEmpty(field109) && string.IsNullOrEmpty(field110) /*&&
                    string.IsNullOrEmpty(field22)*/
                                                   )) {
                message = "Статус составителя расчетного документа не указан (заполнено одно или несколько налоговых полей)";
                return false;
            }

            if (string.IsNullOrEmpty(field101)) return true;

            // Перевод денежных средств в уплату платежей в бюджетную систему РФ
            bool isBudget = IsBudgetPayment(accountR, this.ubs);
            bool is40101 = accountR.StartsWith("40101", StringComparison.Ordinal);

            string field22 = uin; // Поле Код может содержать и УИН и УИП
            if (!string.IsNullOrEmpty(uidPayment) && (string.IsNullOrEmpty(field22) || "0".Equals(field22, StringComparison.Ordinal))) field22 = uidPayment;

            bool isCheck = false;

            if (this.settingTaxFieldsCheck.TryGetValue("Статус составителя расчетного документа", out isCheck) && isCheck) {
                string source = null;
                if (this.settingTaxFieldsAllowableValues.TryGetValue("Статус составителя расчетного документа", out source)
                    && !string.IsNullOrEmpty(source) && !IsContains(source, field101)) {
                    message = string.Format("Статус составителя расчетного документа д.б. '{0}'", source);
                    return false;
                }
            }

            // Для физиков КПП не проверяем
            string straccount_p_db = accountP;
            if (string.IsNullOrEmpty(straccount_p_db)) straccount_p_db = accountDb;
            if (!straccount_p_db.Equals(this.ubsOdAccount.StrAccount, StringComparison.OrdinalIgnoreCase)) this.ubsOdAccount.ReadF(straccount_p_db);

            bool checkKppPayer = true, payerIsFzl = false, payerIsUrOrIp = false;
            if (this.ubsOdAccount.IdClient > 0) {
                if (this.ubsOdAccount.IdClient != this.ubsComClient.Id) this.ubsComClient.Read(this.ubsOdAccount.IdClient);
                if (this.ubsComClient.Type == 2) { checkKppPayer = false; payerIsFzl = true; }
                if (this.ubsComClient.Type == 1 || this.ubsComClient.Type == 2 && this.ubsComClient.Sign == 2) { payerIsUrOrIp = true; }
            }

            if (this.settingTaxFieldsCheck.TryGetValue("КПП плательщика", out isCheck) && isCheck && (checkKppPayer || isBudget)) {
                
                if (string.IsNullOrEmpty(field102) || field102.Length != 9 && !"0".Equals(field102, StringComparison.OrdinalIgnoreCase)) {
                    message = "КПП плательщика должен содержать 9 символов или \"0\"";
                    return false;
                }

                if (isBudget) {
                    if (field102.StartsWith("00", StringComparison.Ordinal)) {
                        message = "КПП плательщика в первом и во втором знаке не может одновременно принимать значение \"0\" (перевод денежных средств в уплату платежей в бюджетную систему РФ)";
                        return false;
                    }
                }

                if (!CheckKppPayer(straccount_p_db, field102, field101, out message)) {
                    return false;
                }
            }

            if (this.settingTaxFieldsCheck.TryGetValue("КПП получателя", out isCheck) && isCheck) {
                if (isBudget) {
                    if (string.IsNullOrEmpty(field103) || field103.Length != 9) {
                        message = "КПП получателя должен содержать 9 символов (перевод денежных средств в уплату платежей в бюджетную систему РФ)";
                        return false;
                    }

                    if (field103.StartsWith("00", StringComparison.OrdinalIgnoreCase)) {
                        message = "КПП получателя в первом и во втором знаке не может одновременно принимать значение \"0\" (перевод денежных средств в уплату платежей в бюджетную систему РФ)";
                        return false;
                    }
                }
                else if (string.IsNullOrEmpty(field103) || field103.Length != 9 && !"0".Equals(field103, StringComparison.OrdinalIgnoreCase)) {
                    message = "КПП получателя должен содержать 9 символов или \"0\"";
                    return false;
                }

                // 0000PP000 - P - 0-1 A-Z
                if (!string.IsNullOrEmpty(field103) && !"0".Equals(field103, StringComparison.OrdinalIgnoreCase)) {
                    if (!Regex.IsMatch(field103, "\\d{4}[0-9A-Z]{2}\\d{3}")) {
                        message = string.Format("КПП получателя <{0}> указан неверно", field103);
                        return false;
                    }
                }
            }

            // В случае перечисления в доход бюджета государственной пошлины по КБК, не администрируемому налоговыми органами, по мнению Департамента, 
            // в поле 104 расчетного документа следует указывать показатель кода бюджетной классификации (КБК) в соответствии с бюджетной классификацией
            // Российской Федерации, в поле 105 - значение кода ОКАТО, на территории которого мобилизуются денежные средства, в полях 106 - 109 проставляются нули "0",
            // в поле 110 указывается значение "ГП". В соответствии с Приложением N 5 к Приказу N 106н в поле 101 расчетного документа должны быть указаны 
            // следующие значения статуса составителя расчетного документа:
            // при перечислении физическими лицами - владельцами текущих счетов налоговых платежей и сборов, администрируемых налоговыми органами, - "13";
            // при перечислении иных платежей в бюджетную систему Российской Федерации (кроме платежей, администрируемых налоговыми органами) - "08".
            if (this.settingTaxFieldsCheck.TryGetValue("Код бюджетной классификации", out isCheck) && isCheck) {
                // Проверяем КБК для 08 и 13 статуса составителя, ну либо проверяем если он передан.
                if ("08".Equals(field101, StringComparison.OrdinalIgnoreCase) || "13".Equals(field101, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(field104)) {
                    if (!CheckChars(field104, allowableChars, false, out message)) {
                        message = "Код бюджетной классификации содержит недопустимые символы либо КБК не задан" + message;
                        return false;
                    }
                    // Код бюджетной классификации (104) - 20 символов или "0" для 40314
                    if (field104.Length != 20 &&
                        !("0".Equals(field104, StringComparison.OrdinalIgnoreCase) &&
                           (accountR.StartsWith("40302", StringComparison.OrdinalIgnoreCase) ||
                            accountR.StartsWith("40501", StringComparison.OrdinalIgnoreCase) ||
                            accountR.StartsWith("40601", StringComparison.OrdinalIgnoreCase) ||
                            accountR.StartsWith("40701", StringComparison.OrdinalIgnoreCase) ||
                            accountR.StartsWith("40503", StringComparison.OrdinalIgnoreCase) ||
                            accountR.StartsWith("40603", StringComparison.OrdinalIgnoreCase) ||
                            accountR.StartsWith("40703", StringComparison.OrdinalIgnoreCase)
                        //accountR.StartsWith("40314", StringComparison.OrdinalIgnoreCase) ||
                            ))) {
                        message = "Код бюджетной классификации должен содержать 20 символов. Допускается заполнение поля КБК значением \"0\" для 40302,40501,40601,40701,40503,40603,40703";
                        return false;
                    }
                }
                else {
                    if (string.IsNullOrEmpty(field104) || field104.Length != 20 && !"0".Equals(field104, StringComparison.OrdinalIgnoreCase)) {
                        message = "Код бюджетной классификации должен содержать 20 символов или \"0\"";
                        return false;
                    }
                    if (!CheckChars(field104, allowableChars, false, out message)) {
                        message = "Код бюджетной классификации содержит недопустимые символы либо КБК не задан" + message;
                        return false;
                    }
                }

                if ("00000000000000000000".Equals(field104, StringComparison.OrdinalIgnoreCase)) {
                    message = "Код бюджетной классификации задан с недопустимым значением <00000000000000000000>";
                    return false;
                }
            }

            bool isFTCRussia = false;
            foreach (string item in this.settingBicAndAccountFTCRussia)
                if (!string.IsNullOrEmpty(bicExtBank) && !string.IsNullOrEmpty(accountR) &&
                    item.StartsWith(bicExtBank, StringComparison.OrdinalIgnoreCase) && item.EndsWith(accountR, StringComparison.OrdinalIgnoreCase)) {
                    isFTCRussia = true;
                    break;
                }

            if (this.settingTaxFieldsCheck.TryGetValue("ОКТМО", out isCheck) && isCheck) {
                if (!CheckChars(field105, allowableDigChars, false, out message)) {
                    message = "Код ОКТМО содержит недопустимые символы либо код ОКТМО не задан" + message;
                    return false;
                }

                if ("0".Equals(field105, StringComparison.OrdinalIgnoreCase)) {
                    if (is40101) {
                        message = "Код ОКТМО не может принимать значение \"0\" (перевод денежных средств в уплату платежей в бюджетную систему РФ, 40101)";
                        return false;
                    }
                }
                else {
                    if (field105.Length != 11 && field105.Length != 8) {
                        message = "Код ОКТМО должен содержать 8/11 символов или \"0\"";
                        return false;
                    }
                    if (string.IsNullOrEmpty(field105.Replace("0", ""))) {
                        message = string.Format("Код ОКТМО задан с недопустимым значением <{0}>", field105);
                        return false;
                    }
                }
            }

            if (this.settingTaxFieldsCheck.TryGetValue("Основание налогового платежа", out isCheck) && isCheck) {
                string source = null;
                if (this.settingTaxFieldsAllowableValues.TryGetValue("Основание налогового платежа", out source)
                    && !string.IsNullOrEmpty(source) && !IsContains(source, field106)) {
                    message = string.Format("Основание налогового платежа д.б. '{0}' или \"0\"", source);
                    return false;
                }
            }

            if (this.settingTaxFieldsCheck.TryGetValue("Налоговый период", out isCheck) && isCheck) {
                if (isFTCRussia) {
                    if (!CheckChars(field107, allowableDigChars, false, out message)) {
                        message = "Показатель налогового периода содержит недопустимые символы либо не задан" + message;
                        return false;
                    }
                    if (field107.Length != 8) {
                        message = "Показатель налогового периода для таможенного платежа должен содержать код таможенного органа (8 символов)";
                        return false;
                    }
                }
                else // Если не таможенный платеж
                {
                    if (!"0".Equals(field107, StringComparison.OrdinalIgnoreCase)) {
                        if (field107.Length != 10) {
                            message = "Показатель налогового периода должен содержать 10 символов или \"0\"";
                            return false;
                        }

                        bool isDate = Regex.IsMatch(field107.Substring(0, 2), "\\d\\d");

                        string source = null;
                        if (this.settingTaxFieldsAllowableValues.TryGetValue("Показатель налогового периода (1-2 знак)", out source)
                            && !string.IsNullOrEmpty(source) && !IsContains(source, field107.Substring(0, 2)) && !isDate) {
                            message = string.Format("Показатель налогового периода документа (XX.mm.yyyy), первые два символа д.б. '{0}'", source);
                            return false;
                        }

                        if (isDate) {
                            DateTime value;
                            if (!DateTime.TryParseExact(field107, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out value)) {
                                message = string.Format("Показатель налогового периода документа '{0}' не в формате даты, разделенной точкой (dd.MM.yyyy)", field107);
                                return false;
                            }
                        }
                        else {
                            Match m = Regex.Match(field107, "(\\S{2})\\.(\\d{2})\\.(\\d{4})");
                            if (string.IsNullOrEmpty(m.Value)) {
                                message = string.Format("Показатель налогового периода документа '{0}' не в формате АА.MM.YYYY", field107);
                                return false;
                            }
                            string aa = m.Groups[1].Value;
                            switch (aa) {
                                case "МС": // - месячные платежи;
                                case "КВ": // - квартальные платежи;
                                case "ПЛ": // - полугодовые платежи;
                                case "ГД": // - годовые
                                    break;
                                default:
                                    message = string.Format("Показатель налогового периода документа '{0}' должен быть одним из МС, КВ, ПЛ, ГД", field107);
                                    return false;
                            }

                            int month, year;
                            if (!int.TryParse(m.Groups[2].Value, out month)) {
                                message = string.Format("Показатель налогового периода документа '{0}' не в формате AA.MM.YYYY (месяц указан не верно)", field107);
                                return false;
                            }

                            if ("ГД".Equals(aa, StringComparison.OrdinalIgnoreCase)) {
                                if (month != 0) {
                                    message = string.Format("Показатель налогового периода документа '{0}' не в формате AA.MM.YYYY (месяц указан не верно, для ГД месяц должен быть равен 00)", field107);
                                    return false;
                                }
                            }
                            else if ("МС".Equals(aa, StringComparison.OrdinalIgnoreCase)) {
                                if (month < 1 || month > 12) {
                                    message = string.Format("Показатель налогового периода документа '{0}' не в формате AA.MM.YYYY (номер месяца указан не верно, д.б. 01-12)", field107);
                                    return false;
                                }
                            }
                            else if ("КВ".Equals(aa, StringComparison.OrdinalIgnoreCase)) {
                                if (month < 1 || month > 4) {
                                    message = string.Format("Показатель налогового периода документа '{0}' не в формате AA.MM.YYYY (номер квартала указан не верно, д.б. 01-04)", field107);
                                    return false;
                                }
                            }
                            else if ("ПЛ".Equals(aa, StringComparison.OrdinalIgnoreCase)) {
                                if (month < 1 || month > 2) {
                                    message = string.Format("Показатель налогового периода документа '{0}' не в формате AA.MM.YYYY (номер полугодия указан не верно, д.б. 01-02)", field107);
                                    return false;
                                }
                            }

                            if (!int.TryParse(m.Groups[3].Value, out year) || year < 1990 || year > DateTime.Now.Year + 1) {
                                message = string.Format("Показатель налогового периода документа '{0}' не в формате AA.MM.YYYY (год указан не верно)", field107);
                                return false;
                            }
                        }

                    }
                }
            }

            if (this.settingTaxFieldsCheck.TryGetValue("Номер налогового документа", out isCheck) && isCheck) {
                if (!CheckChars(field108, allowablePaymentChars, false, out message)) {
                    message = "Номер налогового документа содержит недопустимые символы либо номер не задан" + message;
                    return false;
                }

                if (isBudget) {
                    if ("0".Equals(field108, StringComparison.OrdinalIgnoreCase) && !isReestr) {

                        if ((string.IsNullOrEmpty(field22) || "0".Equals(field22, StringComparison.OrdinalIgnoreCase)) &&
                            (string.IsNullOrEmpty(field60) || "0".Equals(field60, StringComparison.OrdinalIgnoreCase))) {

                            if (payerIsFzl) {
                                message = string.Format("Требуется обязательное указание идентификатора сведений о физическом лице в реквизите 108, реквизит 22 <{0}>, реквизит 60 <{1}> (перевод денежных средств в уплату платежей в бюджетную систему РФ)", field22, field60);
                                return false;
                            }
                        }
                    }
                }
            }

            if (this.settingTaxFieldsCheck.TryGetValue("Дата налогового документа", out isCheck) && isCheck) {
                if (!CheckChars(field109, allowableDigChars + ".", false, out message)) {
                    message = "Дата налогового документа содержит недопустимые символы либо дата не задана" + message;
                    return false;
                }

                if (!"0".Equals(field109, StringComparison.OrdinalIgnoreCase)) {
                    if (field109.Length != 10) {
                        message = "Дата налогового документа должна содержать 10 символов или \"0\"";
                        return false;
                    }
                    if (string.IsNullOrEmpty(field109.Replace("0", ""))) {
                        message = string.Format("Дата налогового документа задана с недопустимым значением <{0}>", field109);
                        return false;
                    }

                    if (!"ЗД".Equals(field106, StringComparison.OrdinalIgnoreCase)) {
                        DateTime value;
                        if (!DateTime.TryParseExact(field109, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out value)) {
                            message = string.Format("Дата налогового документа '{0}' не в формате даты, разделенной точкой (dd.MM.yyyy)", field109);
                            return false;
                        }

                        // Значит это дата платежа
                        if (value == dt22220101) {
                            message = string.Format("Дата налогового документа не задана");
                            return false;
                        }
                    }
                }
            }

            if (this.settingTaxFieldsCheck.TryGetValue("Тип налогового платежа", out isCheck) && isCheck) {
                string source = null;
                if (this.settingTaxFieldsAllowableValues.TryGetValue("Тип налогового платежа", out source)
                    && !string.IsNullOrEmpty(source) && !IsContains(source, field110)) {
                    message = string.Format("Тип налогового платежа д.б. '{0}'", source);
                    return false;
                }
            }

            if (this.settingTaxFieldsCheck.TryGetValue("Уникальный идент. начисления/платежа", out isCheck) && isCheck) {
                if (!CheckChars(field22, allowablePaymentChars, false, out message)) {
                    message = "Уникальный идент. начисления/платежа содержит недопустимые символы либо уникальный идент. не задан" + message;
                    return false;
                }

                if ("0".Equals(field22, StringComparison.OrdinalIgnoreCase)) {

                    if (isBudget && !isReestr) {
                        bool field101Check =
                            "09".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                            "10".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                            "11".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                            "12".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                            "13".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                            "14".Equals(field101, StringComparison.OrdinalIgnoreCase);
                        bool field60Check = !string.IsNullOrEmpty(field60) && field60.Length == 12 && !field60.StartsWith("00", StringComparison.OrdinalIgnoreCase);

                        if (field101Check) {
                            if (!field60Check) {
                                message = string.Format("Уникальный идент. начисления/платежа задан с недопустимым значением \"0\", реквизит 101 <{0}>, реквизит 60 <{1}> (перевод денежных средств в уплату платежей в бюджетную систему РФ)", field101, field60);
                                return false;
                            }
                        }

                        if ("0".Equals(field108, StringComparison.OrdinalIgnoreCase)) {
                            field101Check =
                                "03".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                                "16".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                                "19".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                                "20".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                                "24".Equals(field101, StringComparison.OrdinalIgnoreCase);

                            if (field101Check) {
                                if (!field60Check) {
                                    message = string.Format("Уникальный идент. начисления/платежа задан с недопустимым значением \"0\", реквизит 101 <{0}>, реквизит 60 <{1}>, реквизит 108 <{2}> (перевод денежных средств в уплату платежей в бюджетную систему РФ)", field101, field60, field108);
                                    return false;
                                }
                            }
                        }
                    }
                }
                else {
                    if (field22.Length != 20 && field22.Length != 25) {
                        message = "Уникальный идент. начисления/платежа должен быть \"0\" либо содержать 20 или 25/25 символов";
                        return false;
                    }
                    if (string.IsNullOrEmpty(field22.Replace("0", ""))) {
                        message = string.Format("Уникальный идент. начисления/платежа задан с недопустимым значением <{0}>", field22);
                        return false;
                    }

                    if (field22.Length == 20) {
                        if (!CheckKeyUIN(field22)) {
                            message = string.Format("Уникальный идентификатор начисления задан с недопустимым значением <{0}>, проверка контрольного разряда", field22);
                            return false;
                        }
                    }


                    if (field22.Length == 25) { // Это уникальный идентификатор платежа
                        // Счет N 40822 "Счет для идентификации платежа"
                        if (string.IsNullOrEmpty(accountR) && accountR.StartsWith("40822")) {
                            if (!CheckKeyUIDPayment(straccount_p_db, field22)) {
                                message = string.Format("Уникальный идентификатор платежа задан с недопустимым значением <{0}>", field22);
                                return false;
                            }
                        }
                    }
                }
            }

       

            if (this.settingTaxFieldsCheck.TryGetValue("ИНН плательщика", out isCheck) && isCheck) {
                if (isBudget) {
                    if (!CheckChars(field60, allowableDigChars, false, out message)) {
                        message = "ИНН плательщика содержит недопустимые символы либо ИНН плательщика не задан" + message;
                        return false;
                    }
                    if (!"0".Equals(field60, StringComparison.OrdinalIgnoreCase)) {
                        if (field60.Length != 5 && field60.Length != 10 && field60.Length != 12) {
                            message = "ИНН плательщика должен содержать 5/10/12 символов или \"0\"";
                            return false;
                        }

                        if (string.IsNullOrEmpty(field60.Replace("0", ""))) {
                            message = string.Format("ИНН плательщика задан с недопустимым значением <{0}> (перевод денежных средств в уплату платежей в бюджетную систему РФ)", field60);
                            return false;
                        }

                        if (field60.StartsWith("00", StringComparison.OrdinalIgnoreCase) && (field60.Length == 10 || field60.Length == 12)) {
                            message = string.Format("ИНН плательщика задан с недопустимым значением <{0}>, первые два знака ИНН плательщика не могут одновременно принимать значение \"0\", реквизит 101 <{1}> (перевод денежных средств в уплату платежей в бюджетную систему РФ)", field60, field101);
                            return false;
                        }
                    }
                    else {
                        bool field101Check =
                           "09".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                           "10".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                           "11".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                           "12".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                           "13".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                           "14".Equals(field101, StringComparison.OrdinalIgnoreCase);
                        bool field22Check = !string.IsNullOrEmpty(field22) && !"0".Equals(field22, StringComparison.OrdinalIgnoreCase);

                        if (field101Check) {
                            if (!field22Check) {
                                message = string.Format("ИНН плательщика задан с недопустимым значением \"0\", реквизит 101 <{0}>, реквизит 22 <{1}> (перевод денежных средств в уплату платежей в бюджетную систему РФ)", field101, field22);
                                return false;
                            }
                        }

                        if (payerIsUrOrIp) {
                            message = "ИНН плательщика (юр. лица или индивидуального предпренимателя) задан с недопустимым значением \"0\" (перевод денежных средств в уплату платежей в бюджетную систему РФ)";
                            return false;
                        }
                    }
                }
            }

            if (this.settingTaxFieldsCheck.TryGetValue("ИНН получателя", out isCheck) && isCheck) {
                if (isBudget) {
                    if (!CheckChars(field61, allowableDigChars, false, out message)) {
                        message = "ИНН получателя содержит недопустимые символы либо ИНН получателя не задан" + message;
                        return false;
                    }

                    if (field61.Length != 10) {
                        message = "ИНН получателя должен содержать 10 символов (перевод денежных средств в уплату платежей в бюджетную систему РФ)";
                        return false;
                    }

                    if (!"0".Equals(field61, StringComparison.OrdinalIgnoreCase)) {
                        if (string.IsNullOrEmpty(field61.Replace("0", ""))) {
                            message = string.Format("ИНН получателя задан с недопустимым значением <{0}> (перевод денежных средств в уплату платежей в бюджетную систему РФ)", field61);
                            return false;
                        }
                    }

                    if (field61.StartsWith("00", StringComparison.OrdinalIgnoreCase)) {
                        message = string.Format("ИНН получателя задан с недопустимым значением <{0}>, первые два знака ИНН получателя не могут одновременно принимать значение \"0\", реквизит 101 <{1}> (перевод денежных средств в уплату платежей в бюджетную систему РФ)", field61, field101);
                        return false;
                    }
                }
            }

            if (isReestr && isBudget) {
                if ((string.IsNullOrEmpty(field22) || "0".Equals(field22, StringComparison.Ordinal))) {
                    if ((string.IsNullOrEmpty(uidPayer) || "0".Equals(uidPayer, StringComparison.Ordinal))) {

                        bool field101Check =
                            "03".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                            "16".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                            "19".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                            "20".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                            "24".Equals(field101, StringComparison.OrdinalIgnoreCase);
                        bool field60Check = !string.IsNullOrEmpty(field60) && field60.Length == 12 && !field60.StartsWith("00", StringComparison.OrdinalIgnoreCase);

                        if (field101Check) {
                            if (!field60Check) {
                                message = string.Format("Требуется обязательное указание идентификатора плательщика, реквизит 22 <{0}>, реквизит 60 <{1}> (перевод денежных средств в уплату платежей в бюджетную систему РФ)", field22, field60);
                                return false;
                            }
                        }
                    }

                    {
                        bool field101Check =
                           "09".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                           "10".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                           "11".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                           "12".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                           "13".Equals(field101, StringComparison.OrdinalIgnoreCase) ||
                           "14".Equals(field101, StringComparison.OrdinalIgnoreCase);
                        bool field60Check = !string.IsNullOrEmpty(field60) && field60.Length == 12 && !field60.StartsWith("00", StringComparison.OrdinalIgnoreCase);

                        if (field101Check) {
                            if (!field60Check) {
                                message = string.Format("Уникальный идент. начисления/платежа (по реестру) задан с недопустимым значением \"0\", реквизит 101 <{0}>, реквизит 60 <{1}>, реквизит 108 <{2}> (перевод денежных средств в уплату платежей в бюджетную систему РФ)", field101, field60, field108);
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }
    
     
        /// <summary>
        /// Определяет необходимость КВВО
        /// </summary>
        /// <returns>Признак необходимости валютного котроля (true)</returns>
        /// <exception cref="InvalidOperationException">Номер счета ДБ или КР пуст или длина счета неверна</exception>
        [Obsolete("Следует использовать IsNeedForeignExchangeControlDoc", true)]
        public bool IsNeedForeignExchangeControl() {
            return IsNeedForeignExchangeControl(this.document.Account_DB, this.document.Account_R, this.document.Account_CR);
        }

        /// <summary>
        /// Определяет необходимость КВВО в назначении платежа
        /// </summary>
        /// <returns>Признак необходимости валютного котроля в документе (true)</returns>
        /// <exception cref="InvalidOperationException">Номер счета ДБ или КР пуст или длина счета неверна</exception>
        public bool IsNeedForeignExchangeControlDoc() {
            return GetForeignExchangeControl() > 1;
        }

        private int GetForeignExchangeControl() {
            string recipientAccount = document.KindDoc == 8 ? (string)document.Field("N сч. получателя") : document.Account_R;
            if (string.IsNullOrEmpty(recipientAccount)) recipientAccount = document.Account_CR;

            if ((string.IsNullOrEmpty(recipientAccount) || "00000000000000000000".Equals(recipientAccount)) && this.document.PayLocate != 0) {
                return 0;
            }

            if (document.KindDoc == 6) return 0; // Согласно 138-И

            return GetForeignExchangeControl(this.document.Account_DB, recipientAccount, this.document.Account_CR);
        }

        /// <summary>
        /// Определяет необходимость КВВО в назначении платежа
        /// </summary>
        /// <param name="accountDb">Номер счета ДБ</param>
        /// <param name="accountR">Номер счета получателя</param>
        /// <param name="accountCr">Номер счета КР</param>
        /// <returns>Признак необходимости указания вида валютной операции и признака валютного контроля (true)</returns>
        public bool IsNeedForeignExchangeControl(string accountDb, string accountR, string accountCr) {
            return GetForeignExchangeControl(accountDb, accountR, accountCr) > 1;
        }

        /// <summary>
        /// Получить вариант требования КВВО (в документе/в документе и назначении платежа) 
        /// </summary>
        /// <param name="accountDb">Номер счета ДБ</param>
        /// <param name="accountR">Номер счета получателя</param>
        /// <param name="accountCr">Номер счета КР</param>
        /// <returns>0 - КВВО в документе не требуется, 1 - требуется в документе, 2 - требуется в назначении платежа</returns>
        public int GetForeignExchangeControl(string accountDb, string accountR, string accountCr) {
            int rv = 0;

            Predicate<string[]> checkByMask =
            datas => {
                return !string.IsNullOrEmpty(Regex.Match(datas[0], "^" + datas[1].Replace('_', '.').Replace("%", ".*"), RegexOptions.IgnoreCase).Value);
            };

            Predicate<string> IsRur =
                numCurrency => {
                    return "810".Equals(numCurrency, StringComparison.OrdinalIgnoreCase) || "643".Equals(numCurrency, StringComparison.OrdinalIgnoreCase);
                };

            if (string.IsNullOrEmpty(accountDb) || accountDb.Length != 20)
                throw new UbsObjectException("Номер счета плательщика(ДБ) пуст или длина счета неверна");

            string accountA = accountDb;
            string accountB = null;

            if (string.IsNullOrEmpty(accountR)) {
                if (string.IsNullOrEmpty(accountCr) || accountCr.Length != 20)
                    throw new UbsObjectException("Номер счета получателя(КР) пуст или длина счета неверна");
                accountB = accountCr;
            }
            else {
                accountB = accountR;
                if (accountR.Length != 20)
                    throw new UbsObjectException("Длина счета плательщика неверна (д.б. 20 символов)");
            }

            if (settingMaskAccountNFEC == null) return rv;

            bool result = false;
            for (int i = 0; !result && i <= settingMaskAccountNFEC.GetUpperBound(1); i++) {
                switch (Convert.ToByte(settingMaskAccountNFEC[2, i])) {
                    case 0: { // ДБ
                            bool isRur = IsRur(accountA.Substring(5, 3));
                            result = checkByMask(new string[] { accountA, ((string)settingMaskAccountNFEC[0, i]).Trim() }) &&
                                     checkByMask(new string[] { accountB, ((string)settingMaskAccountNFEC[3, i]).Trim() }) &&
                                     (
                                        (int)settingMaskAccountNFEC[1, i] == 0 ||
                                        (int)settingMaskAccountNFEC[1, i] == 1 && isRur ||
                                        (int)settingMaskAccountNFEC[1, i] == 2 && !isRur
                                     );
                        }
                        break;
                    case 1: { // КР
                            bool isRur = IsRur(accountB.Substring(5, 3));
                            result = checkByMask(new string[] { accountB, ((string)settingMaskAccountNFEC[0, i]).Trim() }) &&
                                     checkByMask(new string[] { accountA, ((string)settingMaskAccountNFEC[3, i]).Trim() }) &&
                                     (
                                        (int)settingMaskAccountNFEC[1, i] == 0 ||
                                        (int)settingMaskAccountNFEC[1, i] == 1 && isRur ||
                                        (int)settingMaskAccountNFEC[1, i] == 2 && !isRur
                                     );


                        }
                        break;
                    default: { // ДБ и КР
                            bool isRur = IsRur(accountA.Substring(5, 3));
                            result = checkByMask(new string[] { accountA, ((string)settingMaskAccountNFEC[0, i]).Trim() }) &&
                                     checkByMask(new string[] { accountB, ((string)settingMaskAccountNFEC[3, i]).Trim() }) &&
                                     (
                                        (int)settingMaskAccountNFEC[1, i] == 0 ||
                                        (int)settingMaskAccountNFEC[1, i] == 1 && isRur ||
                                        (int)settingMaskAccountNFEC[1, i] == 2 && !isRur
                                     );

                            isRur = IsRur(accountB.Substring(5, 3));
                            result |= checkByMask(new string[] { accountB, ((string)settingMaskAccountNFEC[0, i]).Trim() }) &&
                                      checkByMask(new string[] { accountA, ((string)settingMaskAccountNFEC[3, i]).Trim() }) &&
                                      (
                                         (int)settingMaskAccountNFEC[1, i] == 0 ||
                                         (int)settingMaskAccountNFEC[1, i] == 1 && isRur ||
                                         (int)settingMaskAccountNFEC[1, i] == 2 && !isRur
                                      );

                        }
                        break;
                }
            }
            if (!result) return rv;

            rv = 1; // КВВО требуется в документе
        

            Predicate<int> IsExclude = paramNotUsed => {
                // Требуется КВВО, проверим не попадают счета под исключаемые.
                if (settingNFECInNoteDocumentExclude != null) {
                    for (int j = 0; j <= settingNFECInNoteDocumentExclude.GetUpperBound(1); j++) {
                        if (
                                checkByMask(new string[] { accountA, (string)settingNFECInNoteDocumentExclude[0, j] }) &&
                                (
                                    (int)settingNFECInNoteDocumentExclude[1, j] == 0 ||
                                    (int)settingNFECInNoteDocumentExclude[1, j] == 1 && IsRur(accountA.Substring(5, 3)) ||
                                    (int)settingNFECInNoteDocumentExclude[1, j] == 2 && !IsRur(accountA.Substring(5, 3))
                                ) &&
                                checkByMask(new string[] { accountB, (string)settingNFECInNoteDocumentExclude[2, j] }) &&
                                (
                                    (int)settingNFECInNoteDocumentExclude[3, j] == 0 ||
                                    (int)settingNFECInNoteDocumentExclude[3, j] == 1 && IsRur(accountB.Substring(5, 3)) ||
                                    (int)settingNFECInNoteDocumentExclude[3, j] == 2 && !IsRur(accountB.Substring(5, 3))
                                )
                            ) return true;
                    }
                }
                return false;
            };


            if (settingNFECInNoteDocument != null) {
                for (int i = 0; i <= settingNFECInNoteDocument.GetUpperBound(1); i++) {
                    if (
                            checkByMask(new string[] { accountA, (string)settingNFECInNoteDocument[0, i] }) &&
                            (
                                (int)settingNFECInNoteDocument[1, i] == 0 ||
                                (int)settingNFECInNoteDocument[1, i] == 1 && IsRur(accountA.Substring(5, 3)) ||
                                (int)settingNFECInNoteDocument[1, i] == 2 && !IsRur(accountA.Substring(5, 3))
                            ) &&
                            checkByMask(new string[] { accountB, (string)settingNFECInNoteDocument[2, i] }) &&
                            (
                                (int)settingNFECInNoteDocument[3, i] == 0 ||
                                (int)settingNFECInNoteDocument[3, i] == 1 && IsRur(accountB.Substring(5, 3)) ||
                                (int)settingNFECInNoteDocument[3, i] == 2 && !IsRur(accountB.Substring(5, 3))
                            )
                        ) {
                        // Требуется КВВО, проверим не попадают счета под исключаемые.

                        if (IsExclude(0)) continue;
                        return rv + 2;
                    }
                }
            }


            return rv;
        }

        #region Формирование наименования плательщика, получателя

        /// <summary>
        /// Определяет принадлежность счета к банковским счетам и счетам вкладов клиентов
        /// </summary>
        /// <param name="numAccount">Номер счета</param>
        /// <returns>Возвращает признак принадлежности</returns>
        [Obsolete("Метод более не поддерживается, следует использовать метод IsCustomerAccount", true)]
        public bool IsAccClientsDoc(string numAccount) {
            return IsCustomerAccount(this.settingBalAccClients, numAccount);
        }

        /// <summary>
        /// Получить тип наименования плательщика/получателя по номеру счета
        /// </summary>
        /// <param name="numAccount">Номер счета</param>
        /// <param name="typeDef">Используемый тип наименования (0 - наименование клиента, 1 - наименование счета, 2 - наименование банка)</param>
        /// <returns>Признак необходимости применения типа</returns>
        public bool GetTypeNamePayerRecipient(string numAccount, out int typeDef) {
            // Определяем наименование по умолчанию
            return settingDefaultNamePaymentRecipient.TryGetValue(numAccount.Substring(0, 5), out typeDef);
        }
        /// <summary>
        /// Определяет принадлежность счета к банковским счетам и счетам вкладов клиентов
        /// </summary>
        /// <param name="numAccount">Номер счета</param>
        /// <returns>Возвращает признак принадлежности</returns>
        public bool IsCustomerAccount(string numAccount) {
            return IsCustomerAccount(this.settingBalAccClients, numAccount);
        }
        private static bool IsCustomerAccount(List<string> settingBalAccClients, string numAccount) {
            foreach (string balAccClient in settingBalAccClients) {
                if (!string.IsNullOrEmpty(balAccClient) && numAccount.StartsWith(balAccClient, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        private static bool GetDefaultNamePayerRecipient(Dictionary<string, int> settingDefaultNamePaymentRecipient, string settingNamePaymentDocument,
            string settingInnBank, string settingKppBank, string settingOkatoBank,
            string numAccount, string nameAccount,
            string nameClient, string innClient, string kppClient, string okatoClient,
            out string name, out string inn, out string kpp, out string okato) {

            bool byClient = false;
            name = inn = kpp = okato = null;

            // Определяем наименование по умолчанию
            int typeDef = 0;
            if (settingDefaultNamePaymentRecipient.TryGetValue(numAccount.Substring(0, 5), out typeDef)) {
                switch (typeDef) {
                    case 0: name = nameClient; inn = innClient; kpp = kppClient; okato = okatoClient; byClient = true; break;
                    case 1: name = nameAccount; inn = settingInnBank; kpp = settingKppBank; okato = settingOkatoBank; break;
                    case 2: name = settingNamePaymentDocument; inn = settingInnBank; kpp = settingKppBank; okato = settingOkatoBank; break;
                }
            }

            if (string.IsNullOrEmpty(name)) { name = nameClient; inn = innClient; kpp = kppClient; okato = okatoClient; byClient = true; } // Клиент
            if (string.IsNullOrEmpty(name)) { name = nameAccount; inn = settingInnBank; kpp = settingKppBank; okato = settingOkatoBank; byClient = false; } // Счет
            if (string.IsNullOrEmpty(name)) { name = settingNamePaymentDocument; inn = settingInnBank; kpp = settingKppBank; okato = settingOkatoBank; byClient = false; } // Банк

            name = name.Replace("  ", " ").Replace("  ", " ");

            return byClient;
        }
        /// <summary>
        /// Получение дополнительных сведений о плательщике согласно виду
        /// заполнения информации о плательщике
        /// </summary>
        /// <param name="client">Клиент - плательщик</param>
        /// <returns>Дополнительные сведения о плательщике</returns>
        public string GetFormFillingInformationP(UbsComClient client) {
            StringBuilder builder = new StringBuilder();
            DateTime dt22220101 = new DateTime(2222, 1, 1);

            Action<string> forEach = new Action<string>(
                nameField => {
                    if ("ДАТА РОЖДЕНИЯ".Equals(nameField, StringComparison.OrdinalIgnoreCase)) {
                        if (client.Birthday != dt22220101)
                            builder.Append(" " + client.Birthday.ToString("dd.MM.yyyy"));
                    }
                    else if ("МЕСТО РОЖДЕНИЯ".Equals(nameField, StringComparison.OrdinalIgnoreCase))
                        builder.Append(" " + Convert.ToString(client.Field("Место рождения")));
                    else if ("АДРЕС РЕГИСТРАЦИИ".Equals(nameField, StringComparison.OrdinalIgnoreCase))
                        builder.Append(" " + this.ubsComLibrary.FormatAddress(client.GetAddress("Адрес регистрации"), null));
                    else if ("АДРЕС ПМЖ".Equals(nameField, StringComparison.OrdinalIgnoreCase))
                        builder.Append(" " + this.ubsComLibrary.FormatAddress(client.GetAddress("Адрес ПМЖ"), null));
                });

            // Основная информация
            if (settingFormFillingInformationP0 != null)
                foreach (string value in this.settingFormFillingInformationP0)
                    forEach(value);

            // Информация в случае отсутствия основной
            if (builder.Length <= 0) {
                if (this.settingFormFillingInformationP1 != null)
                    foreach (string value in this.settingFormFillingInformationP1)
                        forEach(value);
            }

            return builder.ToString().Trim();
        }

        // ПЛАТЕЛЬЩИК

        /// <summary>
        /// Вернуть признак плательщика ИП/Физ.лицо для индивидуального предпринимателя
        /// </summary>
        /// <param name="kindClient">Вид клиента</param>
        /// <param name="signClient">Тип клиента</param>
        /// <param name="numAccount">Номер счета</param>
        /// <returns>Тип клиента</returns>
        public byte GetSignClient(byte kindClient, byte signClient, string numAccount) {
            if (kindClient == 2 && signClient == 2 && this.settingIsNeedClientActivity.Count > 0) { // Установка заполнена, значит используется
                // Установка определяет по балансовому счету вести с ИП как с ИП или как с читстым физиком
                signClient = 1; // Чистый физик
                foreach (string bal in this.settingIsNeedClientActivity) {
                    if (numAccount.StartsWith(bal, StringComparison.OrdinalIgnoreCase)) {
                        signClient = 2; // ИП
                        break;
                    }
                }
            }
            return signClient;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="settingBalAccClients"></param>
        /// <param name="settingDefaultNamePaymentRecipient"></param>
        /// <param name="settingNamePaymentDocument"></param>
        /// <param name="settingInnBank"></param>
        /// <param name="settingKppBank"></param>
        /// <param name="settingOkatoBank"></param>
        /// <param name="settingIsNeedClientActivity"></param>
        /// <param name="numAccount"></param>
        /// <param name="nameAccount"></param>
        /// <param name="numAccountR"></param>
        /// <param name="nameClient"></param>
        /// <param name="innClient"></param>
        /// <param name="kppClient"></param>
        /// <param name="okatoClient"></param>
        /// <param name="kindOfActivityClient"></param>
        /// <param name="kindDoc"></param>
        /// <param name="typeDoc"></param>
        /// <param name="payLocate"></param>
        /// <param name="kindClient"></param>
        /// <param name="signClient"></param>
        /// <param name="address"></param>
        /// <param name="field101"></param>
        /// <param name="oborot"></param>
        /// <param name="settingLimSummaAddInfoPayer"></param>
        /// <param name="bicDb"></param>
        /// <param name="bicCr"></param>
        /// <param name="documentDate"></param>
        /// <param name="settingAdditionalTextPayer"></param>
        /// <param name="uin"></param>
        /// <param name="onlyByClient"></param>
        /// <param name="name"></param>
        /// <param name="inn"></param>
        /// <param name="kpp"></param>
        /// <param name="okato"></param>
        /// <param name="testname"></param>
        /// <param name="ubs"></param>
        private static void MakeNameP(List<string> settingBalAccClients
            , Dictionary<string, int> settingDefaultNamePaymentRecipient
            , string settingNamePaymentDocument, string settingInnBank, string settingKppBank, string settingOkatoBank
            , List<string> settingIsNeedClientActivity
            , string numAccount, string nameAccount
            , string numAccountR
            , string nameClient, string innClient, string kppClient, string okatoClient, string kindOfActivityClient
            , byte kindDoc, byte typeDoc, byte payLocate, byte kindClient, byte signClient, string address
            , string field101
            , decimal oborot, decimal settingLimSummaAddInfoPayer
            , string bicDb, string bicCr, DateTime documentDate, string settingAdditionalTextPayer, string uin
            , bool onlyByClient, IUbsWss ubs
            , out string name, out string inn, out string kpp, out string okato, out string testname) {

            bool innIsEmpty = string.IsNullOrEmpty(innClient) || "0".Equals(innClient, StringComparison.OrdinalIgnoreCase);

            // Установка определяет по балансовому счету вести с ИП как с ИП или как с читстым физиком
            //signClient = GetSignClient(kindClient, signClient, numAccount); // не используется так как статическая

            if (kindClient == 2 && signClient == 2 && settingIsNeedClientActivity.Count > 0) { // Установка заполнена, значит используется
                
                signClient = 1; // Чистый физик
                foreach (string bal in settingIsNeedClientActivity)
                    if (numAccount.StartsWith(bal, StringComparison.OrdinalIgnoreCase)) {
                        signClient = 2; // ИП
                        break;
                    }
            }

            // Определяем наименование по умолчанию для теста
            //UbsODCheckDocument.GetDefaultNamePayerRecipient(settingDefaultNamePaymentRecipient, settingNamePaymentDocument
            //    , settingInnBank, settingKppBank, settingOkatoBank, numAccount, nameAccount, nameClient, innClient, kppClient, okatoClient
            //    , out testname, out inn, out kpp, out okato);
            //testname = ReplaceTrashString(testname);
            if (!string.IsNullOrEmpty(nameClient)) {
                if (kindClient == 2) {
                    // Приказ Минфина России от 12.11.2013 № 107н вступает в силу 04.02.2014 г. 
                    // При формировании распоряжений о переводе денежных средств в уплату платежей в бюджетную систему РФ указывается в реквизитах
                    if (documentDate >= new DateTime(2014, 2, 4) && !string.IsNullOrEmpty(field101) &&
                        (numAccountR.StartsWith("40101", StringComparison.OrdinalIgnoreCase) ||
                         numAccountR.StartsWith("40302", StringComparison.OrdinalIgnoreCase) ||
                         numAccountR.StartsWith("40501", StringComparison.OrdinalIgnoreCase) && numAccountR[13] == '2' ||
                         numAccountR.StartsWith("40503", StringComparison.OrdinalIgnoreCase) && numAccountR[13] == '4' ||
                         numAccountR.StartsWith("40601", StringComparison.OrdinalIgnoreCase) && (numAccountR[13] == '1' || numAccountR[13] == '3') ||
                         numAccountR.StartsWith("40701", StringComparison.OrdinalIgnoreCase) && (numAccountR[13] == '1' || numAccountR[13] == '3') ||
                         numAccountR.StartsWith("40503", StringComparison.OrdinalIgnoreCase) && numAccountR[13] == '4' ||
                         numAccountR.StartsWith("40603", StringComparison.OrdinalIgnoreCase) && numAccountR[13] == '4' ||
                         numAccountR.StartsWith("40703", StringComparison.OrdinalIgnoreCase) && numAccountR[13] == '4')) {

                        if (signClient == 2)
                            nameClient += string.IsNullOrEmpty(kindOfActivityClient) ? " (ИП)" : " (" + kindOfActivityClient + ")";

                        // По приказу №107н от 12.11.2013 в инкассовых в поле "Плательщик" адрес не должен указываться. 
                        if (kindDoc != 6) {
                            if (string.IsNullOrEmpty(uin) || "0".Equals(uin, StringComparison.OrdinalIgnoreCase)) {
                                nameClient += " //" + address + "//";
                            }
                        }
                    }
                    else {
                        if (signClient == 2) {
                            nameClient += string.IsNullOrEmpty(kindOfActivityClient) ? " ИП" : " " + kindOfActivityClient;
                        }

                        if (!string.IsNullOrEmpty(field101) && IsBudgetPayment(numAccountR, ubs) ||
                            (numAccountR.StartsWith("30111", StringComparison.OrdinalIgnoreCase) ||
                             numAccountR.StartsWith("30114", StringComparison.OrdinalIgnoreCase) ||
                             numAccountR.StartsWith("30231", StringComparison.OrdinalIgnoreCase)) && oborot > settingLimSummaAddInfoPayer ||
                             payLocate != 0 && innIsEmpty && oborot > settingLimSummaAddInfoPayer) {

                            nameClient += " //" + address + "//";
                        }
                    }
                }
                else {
                    if (documentDate >= new DateTime(2014, 2, 4) && !string.IsNullOrEmpty(field101) &&
                        (numAccountR.StartsWith("40101", StringComparison.OrdinalIgnoreCase))) {

                    }
                    else {
                        if ((numAccountR.StartsWith("30111", StringComparison.OrdinalIgnoreCase) ||
                              numAccountR.StartsWith("30114", StringComparison.OrdinalIgnoreCase) ||
                              numAccountR.StartsWith("30231", StringComparison.OrdinalIgnoreCase)) && oborot > settingLimSummaAddInfoPayer) {

                            nameClient += " //" + address + "//";
                        }
                    }
                }
            }

            bool byClient;
            if (onlyByClient) {
                // Определяем наименование по умолчанию
                name = nameClient;
                inn = innClient;
                kpp = kppClient;
                okato = okatoClient;
                byClient = true;
            }
            else {
                // Определяем наименование по умолчанию
                byClient = UbsODCheckDocument.GetDefaultNamePayerRecipient(settingDefaultNamePaymentRecipient, settingNamePaymentDocument
                    , settingInnBank, settingKppBank, settingOkatoBank, numAccount, nameAccount, nameClient, innClient, kppClient, okatoClient
                    , out name, out inn, out kpp, out okato);
            }


            name = name.Trim();
            if (name.Length > 160) name = name.Substring(0, 160).Trim();

            testname = name.Split(new string[] { "//" }, StringSplitOptions.RemoveEmptyEntries)[0];
        }
        /// <summary>
        /// Формирование наименования плательщика по документу
        /// </summary>
        public string MakeNameP() {
            var p = new MakeNameParameters(this.document);
            MakeNamePayer(p);
            return p.Name;
        }

/*
        /// <summary>
        /// Формирование наименования плательщика по документу
        /// </summary>
        private bool MakeNameP(out string name, out string testnameLong, out string testnameReduce, out string testnameShort, out string inn, out string kpp, out string okato, out int clientId) {
            byte kindClient = 0, signClient = 0;
            string nameClient = null, nameClientLong = null, nameClientReduce = null, nameClientShort = null, innClient = null, kppClient = null, okatoClient = null, kindOfActivityClient = null;
            string address = null;

            string payerAccount = string.IsNullOrEmpty(this.document.Account_P) ? this.document.Account_DB : this.document.Account_P;
            if (this.ubsOdAccount.ReadF(payerAccount) <= 0)
                throw new UbsObjectException(string.Format("Счет плательщика <{0}> не найден", payerAccount));

            string recipientAccount = string.IsNullOrEmpty(this.document.Account_R) ? this.document.Account_CR : this.document.Account_R;

            clientId = this.ubsOdAccount.IdClient;
            if (this.ubsOdAccount.IdClient > 0) {
                if (this.ubsOdAccount.IdClient != this.ubsComClient.Id) this.ubsComClient.Read(this.ubsOdAccount.IdClient);

                kindClient = this.ubsComClient.Type;
                signClient = this.ubsComClient.Sign;
                nameClient = GetKindClientName(this.ubsComClient); // (this.ubsComClient.Name ?? "").Trim();
                nameClientLong = (this.ubsComClient.Name ?? "").Trim();
                nameClientReduce = (this.ubsComClient.ReduceName ?? "").Trim();
                nameClientShort = (this.ubsComClient.ShortName ?? "").Trim();
                innClient = (this.ubsComClient.INN ?? "").Trim();
                kppClient = this.ubsComClient.KPPU;
                okatoClient = this.ubsComClient.SOATO;
                kindOfActivityClient = ((string)this.ubsComClient.Field("Вид деятельности физ. лица") ?? "").Trim();

                if (this.ubsComClient.Type == 2) // Физик
                    address = GetFormFillingInformationP(this.ubsComClient);
                else if (this.ubsComClient.Type == 1) // Для юр.лица адресс
                    address = this.ubsComLibrary.FormatAddress(this.ubsComClient.GetAddress("Адрес местонахождения"), null);
            }

            string bicCr = string.IsNullOrEmpty(this.document.BicExtBank) ? this.settingBicBank : this.document.BicExtBank;

            UbsODCheckDocument.MakeNameP(this.settingBalAccClients
              , this.settingDefaultNamePaymentRecipient
              , this.settingNamePaymentDocument, this.settingInnBank, this.settingKppBank, this.settingOkatoBank
              , this.settingIsNeedClientActivity, this.ubsOdAccount.StrAccount, this.ubsOdAccount.Name, recipientAccount
              , nameClient, innClient, kppClient, okatoClient, kindOfActivityClient
              , this.document.KindDoc, this.document.TypeDoc, this.document.PayLocate, kindClient, signClient, address
              , (string)document.Field("Статус составителя расчетного документа")
              , this.document.SummaDB, this.settingLimSummaAddInfoPayer, this.settingBicBank, bicCr, this.document.DateDoc, this.settingAdditionalTextPayer
              , out name, out inn, out kpp, out okato, out testnameLong);


            testnameReduce = testnameShort = null;  // Для физ. лиц указывается всегда только полное наименование
            if (kindClient == 1) {
                string ignoringnameClient;

                UbsODCheckDocument.MakeNameP(this.settingBalAccClients
                    , this.settingDefaultNamePaymentRecipient
                    , this.settingNamePaymentDocument, this.settingInnBank, this.settingKppBank, this.settingOkatoBank
                    , this.settingIsNeedClientActivity, this.ubsOdAccount.StrAccount, this.ubsOdAccount.Name, recipientAccount
                    , nameClientLong, innClient, kppClient, okatoClient, kindOfActivityClient
                    , this.document.KindDoc, this.document.TypeDoc, this.document.PayLocate, kindClient, signClient, address
                    , (string)document.Field("Статус составителя расчетного документа")
                    , this.document.SummaDB, this.settingLimSummaAddInfoPayer, this.settingBicBank, bicCr, this.document.DateDoc, this.settingAdditionalTextPayer
                    , out ignoringnameClient, out inn, out kpp, out okato, out testnameLong);

                UbsODCheckDocument.MakeNameP(this.settingBalAccClients
                    , this.settingDefaultNamePaymentRecipient
                    , this.settingNamePaymentDocument, this.settingInnBank, this.settingKppBank, this.settingOkatoBank
                    , this.settingIsNeedClientActivity, this.ubsOdAccount.StrAccount, this.ubsOdAccount.Name, recipientAccount
                    , nameClientReduce, innClient, kppClient, okatoClient, kindOfActivityClient
                    , this.document.KindDoc, this.document.TypeDoc, this.document.PayLocate, kindClient, signClient, address
                    , (string)document.Field("Статус составителя расчетного документа")
                    , this.document.SummaDB, this.settingLimSummaAddInfoPayer, this.settingBicBank, bicCr, this.document.DateDoc, this.settingAdditionalTextPayer
                    , out ignoringnameClient, out inn, out kpp, out okato, out testnameReduce);

                UbsODCheckDocument.MakeNameP(this.settingBalAccClients
                    , this.settingDefaultNamePaymentRecipient
                    , this.settingNamePaymentDocument, this.settingInnBank, this.settingKppBank, this.settingOkatoBank
                    , this.settingIsNeedClientActivity, this.ubsOdAccount.StrAccount, this.ubsOdAccount.Name, recipientAccount
                    , nameClientShort, innClient, kppClient, okatoClient, kindOfActivityClient
                    , this.document.KindDoc, this.document.TypeDoc, this.document.PayLocate, kindClient, signClient, address
                    , (string)document.Field("Статус составителя расчетного документа")
                    , this.document.SummaDB, this.settingLimSummaAddInfoPayer, this.settingBicBank, bicCr, this.document.DateDoc, this.settingAdditionalTextPayer
                    , out ignoringnameClient, out inn, out kpp, out okato, out testnameShort);
            }

            return true;

        }

  */
        /// <summary>
        /// Формирование наименования плательщика
        /// </summary>
        /// <param name="p">Параметры наименования плательщика
        /// Передаются следующие параметры:
        ///     * Дата документа
        ///     * Вид документа
        ///     * Вид клиента
        ///     * Тип клиента
        ///     * ИНН клиента
        ///     * КПП клиента
        ///     * Код ОКАТО клиента
        ///     * Номер счета плательщика
        ///     * Наименование клиента
        ///     * Наименование счета
        ///     * БИК банка плательщика
        ///     * КПП банка плательщика
        ///     * Код ОКАТО банка плательщика
        ///     * БИК банка получателя
        ///     * Статус составителя расчетного документа [поле 101]
        ///     * Сумма документа
        ///     * Адрес клиента [заполняется вызовом GetFormFillingInformationP]
        ///     * Вид деятельности физ. лица
        ///     * Признак плательщика
        /// </param>
        /// <returns>Наименование плательщика</returns>
        [Obsolete("Следует использовать параметризированный метод формирования наименования плательщика void MakeNamePayer(MakeNameParameters p)", true)]
        public string MakeNameP(UbsParam p) {
            string name, inn, kpp, okato;

            DateTime dateDoc = p.Contains("Дата документа") ? (DateTime)p["Дата документа"] : DateTime.Now.Date;
            byte payLocate = Convert.ToByte(p["Признак плательщика"]);
            byte kindDoc = p.Contains("Вид документа") ? Convert.ToByte(p["Вид документа"]) : (byte)0;
            byte typeDoc = p.Contains("Тип документа") ? Convert.ToByte(p["Тип документа"]) : (byte)0;
            byte kindClient = (p.Contains("Вид клиента") ? Convert.ToByte(p["Вид клиента"]) : (byte)0);
            byte signClient = (p.Contains("Тип клиента") ? Convert.ToByte(p["Тип клиента"]) : (byte)0);

            string numAccount = Convert.ToString(p["Номер счета плательщика"]).Trim();
            string nameAccount = Convert.ToString(p["Наименование счета плательщика"]).Trim();
            string numAccountR = Convert.ToString(p["Номер счета получателя"]).Trim();
            string nameClient = Convert.ToString(p["Наименование клиента"]).Trim();

            string innClient = Convert.ToString(p["ИНН клиента"]).Trim();
            string kppClient = Convert.ToString(p["КПП клиента"]).Trim();
            string okatoClient = Convert.ToString(p["Код ОКАТО клиента"]).Trim();
            
            string kindOfActivityClient = Convert.ToString(p["Вид деятельности физ. лица"]);
            string address = Convert.ToString(p["Адрес клиента"]).Trim();

            string field101 = Convert.ToString(p["Статус составителя расчетного документа"]);

            string bicDb = Convert.ToString(p["БИК банка плательщика"]).Trim();
            string innBank = p.Contains("ИНН банка плательщика") ? Convert.ToString(p["ИНН банка плательщика"]).Trim() : this.settingInnBank;
            string kppBank = p.Contains("КПП банка плательщика") ? Convert.ToString(p["КПП банка плательщика"]).Trim() : this.settingKppBank;
            string okatoBank = p.Contains("Код ОКАТО банка плательщика") ? Convert.ToString(p["Код ОКАТО банка плательщика"]).Trim() : this.settingOkatoBank;
            string bicCr = Convert.ToString(p["БИК банка получателя"]).Trim();

            string uin = Convert.ToString(p["УИН"]).Trim();

            decimal oborot = p.Contains("Сумма документа") ? (decimal)p["Сумма документа"] : (decimal)0;
            string ignoringTestName;
            UbsODCheckDocument.MakeNameP(this.settingBalAccClients
                , this.settingDefaultNamePaymentRecipient
                , this.settingNamePaymentDocument, innBank, kppBank, okatoBank
                , this.settingIsNeedClientActivity, numAccount, nameAccount, numAccountR
                , nameClient, innClient, kppClient, okatoClient, kindOfActivityClient
                , kindDoc, typeDoc, payLocate, kindClient, signClient, address, field101
                , oborot, this.settingLimSummaAddInfoPayer, bicDb, bicCr, dateDoc, this.settingAdditionalTextPayer, uin, false, this.ubs
                , out name, out inn, out kpp, out okato, out ignoringTestName);

            p.Add("Наименование плательщика", name);
            p.Add("ИНН плательщика", inn);
            p.Add("КПП плательщика", kpp);
            p.Add("Код ОКАТО плательщика", okato);

            return name;
        }
        /// <summary>
        /// Установить документу аттрибуты плательщика (Наименование, ИНН, КПП)
        /// В случае внутреннего или исходящего документа, если ИНН и Наименование плательщика
        /// не были заполнены ранее, заполняются поля: Наименование, ИНН, КПП
        /// </summary>
        [Obsolete("Метод более не поддерживается, следует использовать метод FillPayerAttributes", true)]
        public void FillAttrP() {
            FillPayerAttributes();
        }
        /// <summary>
        /// Установить документу аттрибуты плательщика (Наименование, ИНН, КПП, ОКТМО)
        /// В случае внутреннего или исходящего документа, если ИНН и Наименование плательщика
        /// не были заполнены ранее, заполняются поля: Наименование, ИНН, КПП, ОКТМО
        /// </summary>
        public void FillPayerAttributes() {
            string name, inn, kpp, okato;
            if (GetPayerAttributes(out name, out inn, out kpp, out okato)) {
                this.document.Name_P = name;
                this.document.INN_P = inn;
                this.document.Field("КПП плательщика", kpp);
                //this.document.Field("Код ОКАТО", okato);
            }
        }
        /// <summary>
        /// Получить для документа аттрибуты плательщика: Наименование, ИНН, КПП
        /// </summary>
        /// <param name="name">Наименование плательщика</param>
        /// <param name="inn">ИНН плательщика</param>
        /// <param name="kpp">КПП плательщика</param>
        /// <returns>True - атрибуты были вычеслены, False - атрибуты были взяты из соответствующих полей документа</returns>
        public bool GetPayerAttributes(out string name, out string inn, out string kpp) {
            string okato;
            return GetPayerAttributes(out name, out inn, out kpp, out okato);
        }


        /// <summary>
        /// Получить для документа аттрибуты плательщика: Наименование, ИНН, КПП, ОКТМО
        /// </summary>
        /// <param name="name">Наименование плательщика</param>
        /// <param name="inn">ИНН плательщика</param>
        /// <param name="kpp">КПП плательщика</param>
        /// <param name="oktmo">ОКТМО плательщика</param>
        /// <returns>True - атрибуты были вычеслены, False - атрибуты были взяты из соответствующих полей документа</returns>
        private bool GetPayerAttributes(out string name, out string inn, out string kpp, out string oktmo) {
            name = this.document.Name_P;
            inn = this.document.INN_P;
            kpp = this.document.Razdel == 0 ? (string)this.document.Field("КПП плательщика") : null;
            oktmo = this.document.Razdel == 0 ? (string)this.document.Field("Код ОКАТО") : null;

            if (this.document.PayLocate == 0 || this.document.PayLocate == 1) // Внутренний или исходящий
            {
                bool existInn = !string.IsNullOrEmpty(this.document.INN_P);
                bool existName = !string.IsNullOrEmpty(this.document.Name_P);

                if (!existInn || !existName) {

                    var p = new MakeNameParameters(this.document);
                    if (MakeNamePayer(p)) {

                        if (!existInn) inn = p.Inn;
                        if (!existName) name = p.Name;

                        //string field101 = Convert.ToString(this.document.Field("Статус составителя расчетного документа"));
                        //if (!string.IsNullOrEmpty(field101)) kpp = payerKpp;
                        if (string.IsNullOrEmpty(kpp)) kpp = p.Kpp;
                        if (string.IsNullOrEmpty(oktmo)) oktmo = p.Oktmo;
                    }

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Параметры формирования наименования плательщика/получателя
        /// </summary>
        public class MakeNameParameters {
            /// <summary>
            /// Конструктор
            /// </summary>
            public MakeNameParameters() {
                this.PayLocate = 255;
                this.IsRBClientName = false;
            }
            /// <summary>
            /// Конструктор
            /// </summary>
            /// <param name="document">Объект документа</param>
            public MakeNameParameters(UbsODPayDoc document) {
                this.DateDoc = document.DateDoc;
                this.KindDoc = document.KindDoc;
                this.TypeDoc = document.TypeDoc;
                this.PayLocate = document.PayLocate;
                this.SummaDebit = document.SummaDB;
                this.AccountDebit = document.Account_DB;
                this.AccountPayer = document.Account_P;
                this.AccountCredit = document.Account_CR;
                this.AccountRecipient = document.Account_R;
                this.BicExtBank = document.BicExtBank;
                this.InnPayer = document.INN_P;
                this.InnRecipient = document.INN_R;
                if (document.Razdel == 0) {
                    this.Field101 = (string)document.Field("Статус составителя расчетного документа");
                    this.KppPayer = (string)document.Field("КПП плательщика");
                    this.KppRecipient = (string)document.Field("КПП получателя");
                    this.KBK = (string)document.Field("Код бюджетной классификации");
                    this.UIN = (string)document.Field("УИН");
                }

                this.IsRBClientName = false;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public MakeNameParameters Clone() {
                MakeNameParameters p = new MakeNameParameters();
                p.AccountCredit = this.AccountCredit;
                p.AccountDebit = this.AccountDebit;
                p.AccountPayer = this.AccountPayer;
                p.AccountRecipient = this.AccountRecipient;
                p.BicExtBank = this.BicExtBank;
                p.Client = this.Client;
                p.ClientId = this.ClientId;
                p.DateDoc = this.DateDoc;
                p.Field101 = this.Field101;
                p.Inn = this.Inn;
                p.KindDoc = this.KindDoc;
                p.Kpp = this.Kpp;
                p.Name = this.Name;
                p.Oktmo = this.Oktmo;
                p.PayLocate = this.PayLocate;
                p.SummaDebit = this.SummaDebit;
                p.TestingName = this.TestingName;
                p.TypeClientName = this.TypeClientName;
                p.TypeDoc = this.TypeDoc;
                p.UIN = this.UIN;
                p.IsRBClientName = this.IsRBClientName;
                p.KBK = this.KBK;
                p.KppPayer = this.KppPayer;
                p.KppRecipient = this.KppRecipient;

                p.InnPayer = this.InnPayer;
                p.InnRecipient = this.InnRecipient;
                return p;
            }

            /// <summary>
            /// Тип формата наименования
            /// </summary>
            public enum TypeName { 
                /// <summary>
                /// По умолчанию
                /// </summary>
                Normal = 0, 
                /// <summary>
                /// Длинный
                /// </summary>
                Long = 1, 
                /// <summary>
                /// Укороченный
                /// </summary>
                Reduce = 2, 
                /// <summary>
                /// Краткий
                /// </summary>
                Short = 3 }
            /// <summary>
            /// Номер счета дебeта
            /// </summary>
            public string AccountDebit { internal get; set; }
            /// <summary>
            /// Номер счета плательщика
            /// </summary>
            public string AccountPayer { internal get; set; }
            /// <summary>
            /// Номер счета кредита
            /// </summary>
            public string AccountCredit { internal get; set; }
            /// <summary>
            /// Номер счета получателя
            /// </summary>
            public string AccountRecipient { internal get; set; }
            /// <summary>
            /// Клиент для которого должно быть вычислено наименование, иначе вычисляется по счету
            /// </summary>
            public UbsComClient Client { internal get; set; }
            /// <summary>
            /// Вид документа
            /// </summary>
            public byte KindDoc { internal get; set; }
            /// <summary>
            /// Тип документа
            /// </summary>
            public byte TypeDoc { internal get; set; }
            /// <summary>
            /// Признак плательщика
            /// </summary>
            public byte PayLocate { internal get; set; }
            /// <summary>
            /// Статус составителя расчетного документа
            /// </summary>
            public string Field101 { internal get; set; }
            /// <summary>
            /// Сумма документа дебит
            /// </summary>
            public decimal SummaDebit { internal get; set; }
            /// <summary>
            /// Дата документа
            /// </summary>
            public DateTime DateDoc { internal get; set; }
            /// <summary>
            /// Бик внешнего банка
            /// </summary>
            public string BicExtBank { internal get; set; }
            /// <summary>
            /// Тип формата наименования клиента
            /// </summary>
            public TypeName TypeClientName { internal get; set; }
            /// <summary>
            /// УИН
            /// </summary>
            public string UIN { get; internal set; }
            /// <summary>
            /// Формировать наименование для ДБО
            /// </summary>
            public bool IsRBClientName { get; set; }
            /// <summary>
            /// ИНН плательщика
            /// </summary>
            public string InnPayer { internal get; set; }
            /// <summary>
            /// ИНН получателя
            /// </summary>
            public string InnRecipient { internal get; set; }
            /// <summary>
            /// КБК
            /// </summary>
            public string KBK { internal get; set; }
            /// <summary>
            /// КПП плательщика
            /// </summary>
            public string KppPayer { internal get; set; }
            /// <summary>
            /// КПП получателя
            /// </summary>
            public string KppRecipient { internal get; set; }

            /// <summary>
            /// Полученное наименование
            /// </summary>
            public string Name { get; internal set; }
            /// <summary>
            /// Полученный ИНН
            /// </summary>
            public string Inn { get; internal set; }
            /// <summary>
            /// Полученный КПП
            /// </summary>
            public string Kpp { get; internal set; }
            /// <summary>
            /// Полученный ОКТМО
            /// </summary>
            public string Oktmo { get; internal set; }
            /// <summary>
            /// Полученное наименование для сравнения
            /// </summary>
            public string TestingName { get; internal set; }
            /// <summary>
            /// Полученный идентификатор клиента
            /// </summary>
            public int ClientId { get; internal set; }

            /// <summary>
            /// Получить счет плательщика или счет дебет
            /// </summary>
            internal string GetAccountPayer() { return (string.IsNullOrEmpty(this.AccountPayer) ? this.AccountDebit : this.AccountPayer) ?? string.Empty; }
            /// <summary>
            /// Получить счет получателя или счет кредит
            /// </summary>
            internal string GetAccountRecipient() { return (string.IsNullOrEmpty(this.AccountRecipient) ? this.AccountCredit : this.AccountRecipient) ?? string.Empty; }
            
        }

        /// <summary>
        /// Получить наименование плательщика
        /// </summary>
        /// <param name="p">Параметры формирования наименования</param>
        public bool MakeNamePayer(MakeNameParameters p) {
            string payerAccount = p.GetAccountPayer();// string.IsNullOrEmpty(p.AccountPayer) ? p.AccountDebit : p.AccountPayer;
            if (this.ubsOdAccount.ReadF(payerAccount) == 0)
                return false;
                //throw new UbsObjectException(string.Format("Счет плательщика <{0}> не найден", payerAccount));

            string recipientAccount = p.GetAccountRecipient(); // (string.IsNullOrEmpty(p.AccountRecipient) ? p.AccountCredit : p.AccountRecipient) ?? string.Empty;
            string bicCr = string.IsNullOrEmpty(p.BicExtBank) ? this.settingBicBank : p.BicExtBank;
            if (p.PayLocate == 255) p.PayLocate = IsLocate(this.settingBicBank, bicCr, recipientAccount) ? (byte)0 : (byte)1;

            decimal rate = 0;
            int nu = 0;
            DateTime dateRate;
            this.ubsComRates.GetRateCB(this.ubsOdAccount.IdCurrency, DateTime.Now.Date, out rate, out nu, out dateRate);
            decimal oborot = p.SummaDebit * rate / (decimal)nu;


            string name, inn, kpp, okato, testingName;
            if ((p.ClientId = this.ubsOdAccount.IdClient) == 0) {

                if (p.IsRBClientName) return false; // Для специализировнной проверки в ДБО формировать только по клиентским счетам

                UbsODCheckDocument.MakeNameP(this.settingBalAccClients
                    , this.settingDefaultNamePaymentRecipient
                    , this.settingNamePaymentDocument, this.settingInnBank, this.settingKppBank, this.settingOkatoBank
                    , this.settingIsNeedClientActivity, this.ubsOdAccount.StrAccount, this.ubsOdAccount.Name, recipientAccount
                    , null, null, null, null, null
                    , p.KindDoc, p.TypeDoc, p.PayLocate, 0, 0, null
                    , p.Field101
                    , oborot, this.settingLimSummaAddInfoPayer, this.settingBicBank, bicCr, p.DateDoc, this.settingAdditionalTextPayer, p.UIN, false, this.ubs
                    , out name, out inn, out kpp, out okato, out testingName);
            }
            else {

                if (p.IsRBClientName && !IsCustomerAccount(this.ubsOdAccount.StrAccount)) return false; // Для специализировнной проверки в ДБО формировать только по клиентским счетам

                if (p.Client == null) { // Если клиент не передан, определяем по счету
                    p.Client = this.ubsComClient;
                }
                if (this.ubsOdAccount.IdClient != p.Client.Id) p.Client.Read(this.ubsOdAccount.IdClient);

                byte kindClient = p.Client.Type;
                byte signClient = p.Client.Sign;
                string nameClient = (p.TypeClientName == MakeNameParameters.TypeName.Normal ? GetKindClientName(p.Client) :
                                    (p.TypeClientName == MakeNameParameters.TypeName.Long ? p.Client.Name :
                                    (p.TypeClientName == MakeNameParameters.TypeName.Reduce ? p.Client.ReduceName : p.Client.ShortName)));
                string innClient = (p.Client.INN ?? string.Empty).Trim();
                string kppClient = (p.Client.KPPU ?? string.Empty).Trim();

                if (!string.IsNullOrEmpty(p.Field101)) {
                    if (string.IsNullOrEmpty(innClient)) innClient = "0";
                    if (string.IsNullOrEmpty(kppClient)) innClient = "0";
                }

                string okatoClient = (p.Client.SOATO ?? string.Empty).Trim();
                string kindOfActivityClient = ((string)p.Client.Field("Вид деятельности физ. лица") ?? string.Empty).Trim();
                string address = p.Client.Type == 2 ? /*физ. лица*/ GetFormFillingInformationP(p.Client) : /*юр.лица*/ this.ubsComLibrary.FormatAddress(p.Client.GetAddress("Адрес местонахождения"), null);

                UbsODCheckDocument.MakeNameP(this.settingBalAccClients
                    , this.settingDefaultNamePaymentRecipient
                    , this.settingNamePaymentDocument, this.settingInnBank, this.settingKppBank, this.settingOkatoBank
                    , this.settingIsNeedClientActivity, this.ubsOdAccount.StrAccount, this.ubsOdAccount.Name, recipientAccount
                    , nameClient, innClient, kppClient, okatoClient, kindOfActivityClient
                    , p.KindDoc, p.TypeDoc, p.PayLocate, kindClient, signClient, address
                    , p.Field101
                    , oborot, this.settingLimSummaAddInfoPayer, this.settingBicBank, bicCr, p.DateDoc, this.settingAdditionalTextPayer, p.UIN, p.IsRBClientName, this.ubs
                    , out name, out inn, out kpp, out okato, out testingName);
            }
          
            p.Name = name;
            p.Inn = inn;
            p.Kpp = kpp;
            p.Oktmo = okato;
            p.TestingName = testingName;
            return true;
        }

        // ПОЛУЧАТЕЛЬ

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settingBalAccClients"></param>
        /// <param name="settingDefaultNamePaymentRecipient"></param>
        /// <param name="settingNamePaymentDocument"></param>
        /// <param name="settingInnBank"></param>
        /// <param name="settingKppBank"></param>
        /// <param name="settingOkatoBank"></param>
        /// <param name="settingIsNeedClientActivity"></param>
        /// <param name="numAccount"></param>
        /// <param name="nameAccount"></param>
        /// <param name="nameClient"></param>
        /// <param name="innClient"></param>
        /// <param name="kppClient"></param>
        /// <param name="okatoClient"></param>
        /// <param name="kindOfActivityClient"></param>
        /// <param name="kindDoc"></param>
        /// <param name="typeDoc"></param>
        /// <param name="payLocate"></param>
        /// <param name="kindClient"></param>
        /// <param name="signClient"></param>
        /// <param name="address"></param>
        /// <param name="onlyByClient"></param>
        /// <param name="name"></param>
        /// <param name="inn"></param>
        /// <param name="kpp"></param>
        /// <param name="okato"></param>
        /// <param name="testname">Имя для теста наименования получателя (без адреса и всего лишнего)</param>
        private static void MakeNameR(List<string> settingBalAccClients
            , Dictionary<string, int> settingDefaultNamePaymentRecipient
            , string settingNamePaymentDocument, string settingInnBank, string settingKppBank, string settingOkatoBank
            , List<string> settingIsNeedClientActivity
            , string numAccount, string nameAccount
            , string nameClient, string innClient, string kppClient, string okatoClient, string kindOfActivityClient
            , byte kindDoc, byte typeDoc, byte payLocate, byte kindClient, byte signClient, string address
            , bool onlyByClient
            , out string name, out string inn, out string kpp, out string okato, out string testname) {

            string clientTypeInfo = "", clientAdditionalInfo = "";

            //bool isAccClient = UbsODCheckDocument.IsCustomerAccount(settingBalAccClients, numAccount);
            if (kindClient == 2 && signClient == 2 && settingIsNeedClientActivity.Count > 0) { // Установка заполнена, значит используется
                // Установка определяет по балансовому счету вести с ИП как с ИП или как с читстым физиком
                signClient = 1; // Чистый физик
                foreach (string bal in settingIsNeedClientActivity)
                    if (numAccount.StartsWith(bal, StringComparison.OrdinalIgnoreCase)) {
                        signClient = 2; // ИП
                        break;
                    }
            }


            #region clientTypeInfo
            if (kindClient == 2 && signClient == 2) {
                clientTypeInfo = kindOfActivityClient;
            }
            #endregion

            #region clientAdditionalInfo

            //bool needAddress = false;

            //if (kindClient == 2 && signClient == 1) needAddress = true;

            //if (needAddress) {
            //    clientAdditionalInfo = "//" + address + "//";
            //}

            #endregion

            // Определяем наименование по умолчанию для теста
            //UbsODCheckDocument.GetDefaultNamePayerRecipient(settingDefaultNamePaymentRecipient, settingNamePaymentDocument
            //    , settingInnBank, settingKppBank, settingOkatoBank, numAccount, nameAccount, nameClient, innClient, kppClient, okatoClient
            //    , out testname, out inn, out kpp, out okato);
            //testname = ReplaceTrashString(testname);


            nameClient = (nameClient ?? "").Trim();
            if (!string.IsNullOrEmpty(clientAdditionalInfo)) nameClient += " " + clientAdditionalInfo.Trim();
            if (!string.IsNullOrEmpty(clientTypeInfo)) nameClient += " " + clientTypeInfo.Trim();

            if (onlyByClient) {
                // Определяем наименование по умолчанию реальное
                name = nameClient;
                inn = innClient;
                kpp = kppClient;
                okato = okatoClient;
            }
            else {
                // Определяем наименование по умолчанию реальное
                UbsODCheckDocument.GetDefaultNamePayerRecipient(settingDefaultNamePaymentRecipient, settingNamePaymentDocument
                    , settingInnBank, settingKppBank, settingOkatoBank, numAccount, nameAccount, nameClient, innClient, kppClient, okatoClient
                    , out name, out inn, out kpp, out okato);
            }

            name = name.Trim();
            if (name.Length > 160) name = name.Substring(0, 160).Trim();

            testname = name.Split(new string[] { "//" }, StringSplitOptions.RemoveEmptyEntries)[0];
        }
        /// <summary>
        /// Формирование наименования получателя по документу
        /// </summary>
        public string MakeNameR() {
            var p = new MakeNameParameters(this.document);
            MakeNameRecipient(p);
            return p.Name;
        }
      
        /// <summary>
        /// Формирование наименования получателя по переданным параметрам
        /// </summary>
        /// <param name="p">Параметры наименования получателя
        /// Передаются следующие параметры:
        ///     * Признак плательщика
        ///     * Вид документа
        ///     * Вид клиента
        ///     * ИНН клиента
        ///     * КПП клиента
        ///     * Номер счета получателя
        ///     * Наименование клиента
        ///     * Наименование счета
        ///     * ИНН банка получателя
        ///     * КПП банка получателя
        /// </param>
        /// <returns>Наименование получателя</returns>
        public string MakeNameR(UbsParam p) {
            string name, inn, kpp, okato;

            byte payLocate = Convert.ToByte(p["Признак плательщика"]);
            byte kindDoc = p.Contains("Вид документа") ? Convert.ToByte(p["Вид документа"]) : (byte)0;
            byte typeDoc = p.Contains("Тип документа") ? Convert.ToByte(p["Тип документа"]) : (byte)0;
            byte kindClient = (p.Contains("Вид клиента") ? Convert.ToByte(p["Вид клиента"]) : (byte)0);
            byte signClient = (p.Contains("Тип клиента") ? Convert.ToByte(p["Тип клиента"]) : (byte)0);
            string numAccount = Convert.ToString(p["Номер счета получателя"]);
            string nameAccount = Convert.ToString(p["Наименование счета"]);

            string nameClient = Convert.ToString(p["Наименование клиента"]);
            string innClient = Convert.ToString(p["ИНН клиента"]);
            string kppClient = Convert.ToString(p["КПП клиента"]);
            string okatoClient = Convert.ToString(p["Код ОКАТО клиента"]);
            
            string kindOfActivityClient = Convert.ToString(p["Вид деятельности физ. лица"]);
            string address = Convert.ToString(p["Адрес клиента"]).Trim();

            string innBank = p.Contains("ИНН банка получателя") ? Convert.ToString(p["ИНН банка получателя"]) : this.settingInnBank;
            string kppBank = p.Contains("КПП банка получателя") ? Convert.ToString(p["КПП банка получателя"]) : this.settingKppBank;
            string okatoBank = p.Contains("Код ОКАТО банка получателя") ? Convert.ToString(p["Код ОКАТО банка получателя"]) : this.settingOkatoBank;

            string ignoretestname;

            UbsODCheckDocument.MakeNameR(this.settingBalAccClients
                , this.settingDefaultNamePaymentRecipient
                , this.settingNamePaymentDocument, innBank, kppBank, okatoBank
                , this.settingIsNeedClientActivity
                , numAccount, nameAccount
                , nameClient, innClient, kppClient, okatoClient, kindOfActivityClient
                , kindDoc, typeDoc, payLocate, kindClient, signClient, address, false, out name, out inn, out kpp, out okato, out ignoretestname);

            p.Add("Наименование получателя", name);
            p.Add("ИНН получателя", inn);
            p.Add("КПП получателя", kpp);
            p.Add("Код ОКАТО получателя", okato);

            return name;
        }
        /// <summary>
        /// Установить документу аттрибуты получателя (Наименование, ИНН, КПП)
        /// В случае внутреннего или входящего документа, если ИНН и Наименование получателя
        /// не были заполнены ранее, заполняются поля: Наименование, ИНН, КПП
        /// ИНН для кассовых ордеров не заполняется, если ИНН в документе пусто
        /// </summary>
        [Obsolete("Метод более не поддерживается, следует использовать метод FillRecipientAttributes", true)]
        public void FillAttrR() { FillRecipientAttributes(); }
        /// <summary>
        /// Установить документу аттрибуты получателя (Наименование, ИНН, КПП)
        /// В случае внутреннего или входящего документа, если ИНН и Наименование получателя
        /// не были заполнены ранее, заполняются поля: Наименование, ИНН, КПП
        /// </summary>
        public void FillRecipientAttributes() {
            string name, inn, kpp, okato;
            if (GetRecipientAttributes(out name, out inn, out kpp, out okato)) {
                this.document.Name_R = name;
                this.document.INN_R = inn;
                this.document.Field("КПП получателя", kpp);

                string field101 = Convert.ToString(this.document.Field("Статус составителя расчетного документа"));
                if (!string.IsNullOrEmpty(field101) || this.document.KindDoc == 9 && this.document.TypeDoc == 1) {
                    if (this.document.Razdel == 0) this.document.Field("Код ОКАТО", okato);
                }
            }
        }
        /// <summary>
        /// Получить для документа аттрибуты получателя: Наименование, ИНН, КПП
        /// </summary>
        /// <param name="name">Наименование плательщика</param>
        /// <param name="inn">ИНН плательщика</param>
        /// <param name="kpp">КПП плательщика</param>
        /// <returns>True - атрибуты были вычеслены, False - атрибуты были взяты из соответствующих полей документа</returns>
        public bool GetRecipientAttributes(out string name, out string inn, out string kpp) {
            string okato;
            return GetRecipientAttributes(out name, out inn, out kpp, out okato);
        }

        private bool GetRecipientAttributes(out string name, out string inn, out string kpp, out string okato) {
            name = this.document.Name_R;
            inn = this.document.INN_R;
            kpp = this.document.Razdel == 0 ? (string)this.document.Field("КПП получателя") : null;
            okato = this.document.Razdel == 0 ? (string)this.document.Field("Код ОКАТО") : null;

            if (this.document.PayLocate == 0 || this.document.PayLocate == 2) // Внутренний или входящий
            {
                bool existInn = !string.IsNullOrEmpty(this.document.INN_R);
                bool existName = !string.IsNullOrEmpty(this.document.Name_R);

                if (!existInn || !existName) {
                    var p = new MakeNameParameters(this.document);
                    if (MakeNameRecipient(p)) {
                        // Для кассовых ордеров не устанавливать принудительно ИНН, если он не задан в документе
                        if (!existInn) inn = p.Inn;
                        if (!existName) name = p.Name;

                        //string field101 = Convert.ToString(this.document.Field("Статус составителя расчетного документа"));
                        //if (!string.IsNullOrEmpty(field101)) kpp = receivedKpp;
                        if (string.IsNullOrEmpty(kpp)) kpp = p.Kpp;
                        if (string.IsNullOrEmpty(okato)) okato = p.Oktmo;

                        return true;
                    }
                }
            }
            return false;
        }


        /// <summary>
        /// Получить наименование получателя
        /// </summary>
        /// <param name="p">Параметры формирования наименования</param>
        public bool MakeNameRecipient(MakeNameParameters p) {
            string recipientAccount = string.IsNullOrEmpty(p.AccountRecipient) ? p.AccountCredit : p.AccountRecipient;
            if (this.ubsOdAccount.ReadF(recipientAccount) <= 0)
                return false;
                //throw new UbsObjectException(string.Format("Счет получателя <{0}> не найден", recipientAccount));

            string name, inn, kpp, okato, testingName;
            if ((p.ClientId = this.ubsOdAccount.IdClient) == 0) {

                // if (p.IsRBClientName) return false; // Для специализировнной проверки в ДБО формировать только по клиентским счетам

                UbsODCheckDocument.MakeNameR(this.settingBalAccClients
                    , this.settingDefaultNamePaymentRecipient
                    , this.settingNamePaymentDocument, this.settingInnBank, this.settingKppBank, this.settingOkatoBank
                    , this.settingIsNeedClientActivity, this.ubsOdAccount.StrAccount, this.ubsOdAccount.Name
                    , null, null, null, null, null
                    , p.KindDoc, p.TypeDoc, p.PayLocate, 0, 0, null, false
                    , out name, out inn, out kpp, out okato, out testingName);
            }
            else {

                // if (p.IsRBClientName && !IsCustomerAccount(this.ubsOdAccount.StrAccount)) return false; // Для специализировнной проверки в ДБО формировать только по клиентским счетам

                if (p.Client == null) { // Если клиент не передан, определяем по счету
                    p.Client = this.ubsComClient;
                }
                if (this.ubsOdAccount.IdClient != p.Client.Id) p.Client.Read(this.ubsOdAccount.IdClient);

                byte kindClient = p.Client.Type;
                byte signClient = p.Client.Sign;
                string nameClient = (p.TypeClientName == MakeNameParameters.TypeName.Normal ? GetKindClientName(p.Client) :
                                    (p.TypeClientName == MakeNameParameters.TypeName.Long ? p.Client.Name :
                                    (p.TypeClientName == MakeNameParameters.TypeName.Reduce ? p.Client.ReduceName : p.Client.ShortName)));
                string innClient = (p.Client.INN ?? string.Empty).Trim();
                string kppClient = (p.Client.KPPU ?? string.Empty).Trim();
                string okatoClient = (p.Client.SOATO ?? string.Empty).Trim();
                string kindOfActivityClient = ((string)p.Client.Field("Вид деятельности физ. лица") ?? string.Empty).Trim();
                string address = p.Client.Type == 2 ? /*физ. лица*/ GetFormFillingInformationP(p.Client) : /*юр.лица*/ this.ubsComLibrary.FormatAddress(p.Client.GetAddress("Адрес местонахождения"), null);

                UbsODCheckDocument.MakeNameR(this.settingBalAccClients
                    , this.settingDefaultNamePaymentRecipient
                    , this.settingNamePaymentDocument, this.settingInnBank, this.settingKppBank, this.settingOkatoBank
                    , this.settingIsNeedClientActivity, this.ubsOdAccount.StrAccount, this.ubsOdAccount.Name
                    , nameClient, innClient, kppClient, okatoClient, kindOfActivityClient
                    , p.KindDoc, p.TypeDoc, p.PayLocate, kindClient, signClient, address, p.IsRBClientName
                    , out name, out inn, out kpp, out okato, out testingName);
            }

            p.Name = name;
            p.Inn = inn;
            p.Kpp = kpp;
            p.Oktmo = okato;
            p.TestingName = testingName;
            return true;
        }


        private static string GetKindNamePayer(UbsComClient client, string settingKindName) {
            string name = client.Name;

            if ("ПОЛНОЕ".Equals(settingKindName, StringComparison.OrdinalIgnoreCase)) {
                name = client.Name;
            }
            else if ("УКОРОЧЕННОЕ".Equals(settingKindName, StringComparison.OrdinalIgnoreCase)) {
                name = client.ReduceName;
            }
            else if ("КРАТКОЕ".Equals(settingKindName, StringComparison.OrdinalIgnoreCase)) {
                name = client.ShortName;
            }
            return name;
        }

        /// <summary>
        /// Получить полное, укороченное или краткое имя клиента согласно значению в установке
        /// </summary>
        /// <param name="client">Клиент</param>
        /// <returns>Наименование клиента</returns>
        public string GetKindClientName(UbsComClient client) {
            string name = client.Name;
            if (client.Type == 1) {
                if ("ПОЛНОЕ".Equals(this.settingKindName, StringComparison.OrdinalIgnoreCase)) {
                    name = client.Name;
                }
                else if ("УКОРОЧЕННОЕ".Equals(this.settingKindName, StringComparison.OrdinalIgnoreCase)) {
                    name = client.ReduceName;
                }
                else if ("КРАТКОЕ".Equals(this.settingKindName, StringComparison.OrdinalIgnoreCase)) {
                    name = client.ShortName;
                }
            }
            return (name ?? string.Empty).Trim();
        }


        #endregion

        /// <summary>
        /// Определяет принадлежность счетов документа к счетам резидентов а также необходимость валютного контроля документа
        /// </summary>
        /// <returns>Признак принадлежности счетов документа к счетам резидентов (true)</returns>
        public bool IsResidentAccounts() {

            if (!string.IsNullOrEmpty(document.Account_DB) && this.settingBal2AccountsNonResident.Contains(document.Account_DB.Substring(0, 5))) return false;
            if (!string.IsNullOrEmpty(document.Account_CR) && this.settingBal2AccountsNonResident.Contains(document.Account_CR.Substring(0, 5))) return false;
            if (!string.IsNullOrEmpty(document.Account_P) && this.settingBal2AccountsNonResident.Contains(document.Account_P.Substring(0, 5))) return false;
            if (!string.IsNullOrEmpty(document.Account_R) && this.settingBal2AccountsNonResident.Contains(document.Account_R.Substring(0, 5))) return false;

            return true;
        }

        #region Формирование строки для электронной подписи для документов и табличных ордеров

        /// <summary>
        /// Получить данные для подписания
        /// </summary>
        /// <param name="iUbsDb">Интерфейс взаимодействия с БД</param>
        /// <param name="iUbsWss">Интерфейс взаимодействия с СП</param>
        /// <param name="procVersion">Версия процедуры получения данных</param>
        /// <param name="typeObject">Тип объекта (0 - документы, 1 - табличные ордера)</param>
        /// <param name="id">Идентификатор документа либо табличного ордера</param>
        /// <param name="razdel">Раздел</param>
        /// <param name="outParameters">Выходные параметры [Вид в типе подписи, Источник создания]</param>
        /// <returns>Данные для подписания</returns>
        public static string GetDataForSigning(IUbsDbConnection iUbsDb, IUbsWss iUbsWss, byte procVersion, int typeObject, int id, byte razdel, out object[] outParameters) {
            outParameters = null;
            string retval = null;

            if (typeObject == 0) {
                byte ssdKinddoc = 0;
                string ssdReference = null;

                retval = GetDataDocumentForSigning(iUbsDb, iUbsWss, id, razdel, procVersion, out ssdKinddoc, out ssdReference);
                outParameters = new object[] { ssdKinddoc, ssdReference };
            }
            else if (typeObject == 1) {
                byte ssdKinddoc = 0;

                retval = GetDataTableOrderForSigning(iUbsDb, iUbsWss, id, razdel, procVersion, out ssdKinddoc);
                outParameters = new object[] { ssdKinddoc, null };
            }

            return retval;
        }
        private static string GetDataDocumentForSigning(IUbsDbConnection iUbsDb, IUbsWss iUbsWss, int idDoc, byte razdel, byte procVersion, out byte ssdKinddok, out string ssdReference) {
            ssdKinddok = 0;
            ssdReference = null;

            UbsODPayDoc payDoc = new UbsODPayDoc(iUbsDb, iUbsWss, razdel);
            payDoc.Clear();
            payDoc.Read(idDoc);

            ssdKinddok = payDoc.KindDoc;

            // Меняем типы для приходного кассового ордера и расходного кассового ордера
            if (payDoc.KindDoc == 9 && payDoc.TypeDoc == 1) ssdKinddok = 91;
            else if (payDoc.KindDoc == 9 && payDoc.TypeDoc == 2) ssdKinddok = 92;

            ssdReference = payDoc.RefSource;

            StringBuilder builder = new StringBuilder();

            builder.Append(string.Format("[Идентификатор документа:{0}]", payDoc.Id));
            builder.Append(string.Format("[Номер документа:{0}]", payDoc.Number));
            builder.Append(string.Format("[Шифр документа ЦБ:{0}]", payDoc.KindDoc));
            builder.Append(string.Format("[Тип документа:{0}]", payDoc.TypeDoc));
            builder.Append(string.Format("[Дата документа:{0}]", ((DateTime)payDoc.DateDoc).ToString("dd.MM.yyyy")));

            builder.Append(string.Format("[Дата проведения по балансу:{0}]", ((DateTime)payDoc.DateTrn).ToString("dd.MM.yyyy")));
            if (procVersion < 2) builder.Append(string.Format("[Признак проведения по балансу:{0}]", payDoc.ExecTrn ? 1 : 0));

            builder.Append(string.Format("[Сумма по дебету:{0}]", (long)(payDoc.SummaDB * 100)));
            builder.Append(string.Format("[Сумма по кредиту:{0}]", (long)(payDoc.SummaCR * 100)));

            if (procVersion < 4)
                builder.Append(string.Format("[Сумма рублевого покрытия:{0}]", (long)(payDoc.SummaRUR * 100)));

            builder.Append(string.Format("[Сумма курсовой разницы:{0}]", (long)(payDoc.SummaRD * 100)));

            if (razdel == 0) builder.Append(string.Format("[Признак плательщика:{0}]", payDoc.PayLocate));

            builder.Append(string.Format("[Счет дебет:{0}]", payDoc.Account_DB));

            if (!(procVersion >= 3 && (payDoc.KindDoc == 1 || payDoc.KindDoc == 2 ||
                                        payDoc.KindDoc == 6 || payDoc.KindDoc == 8 ||
                                        payDoc.KindDoc == 16)))
                builder.Append(string.Format("[Счет кредит:{0}]", payDoc.Account_CR));

            builder.Append(string.Format("[Счет курсовой разницы:{0}]", payDoc.Account_RD));

            if (razdel == 0) {
                builder.Append(string.Format("[Счет плательщика:{0}]", payDoc.Account_P));
                builder.Append(string.Format("[Счет получателя:{0}]", payDoc.Account_R));
            }

            builder.Append(string.Format("[Наименование плательщика:{0}]", payDoc.Name_P));
            builder.Append(string.Format("[ИНН плательщика:{0}]", payDoc.INN_P));
            builder.Append(string.Format("[Наименование получателя:{0}]", payDoc.Name_R));
            builder.Append(string.Format("[ИНН получателя:{0}]", payDoc.INN_R));
            builder.Append(string.Format("[Назначение платежа:{0}]", payDoc.Description));

            if (razdel == 0) {
                builder.Append(string.Format("[Очередность платежа:{0}]", payDoc.PriorityPay));
                builder.Append(string.Format("[БИК внешнего банка:{0}]", payDoc.BicExtBank));
                builder.Append(string.Format("[Коррсчет внешнего банка:{0}]", payDoc.AccountExtBank));
                builder.Append(string.Format("[Наименование внешнего банка:{0}]", payDoc.NameExtBank));
            }

            builder.Append(string.Format("[Номер отделения:{0}]", payDoc.NumDivision));

            if (razdel == 0) {
                builder.Append(string.Format("[Статус составителя расчетного документа:{0}]", payDoc.Field("Статус составителя расчетного документа")));
                builder.Append(string.Format("[КПП плательщика:{0}]", payDoc.Field("КПП плательщика")));
                builder.Append(string.Format("[КПП получателя:{0}]", payDoc.Field("КПП получателя")));
                builder.Append(string.Format("[Код бюджетной классификации:{0}]", payDoc.Field("Код бюджетной классификации")));
                builder.Append(string.Format("[Код ОКАТО:{0}]", payDoc.Field("Код ОКАТО")));
                builder.Append(string.Format("[Основание налогового платежа:{0}]", payDoc.Field("Основание налогового платежа")));
                builder.Append(string.Format("[Налоговый период:{0}]", payDoc.Field("Налоговый период")));
                builder.Append(string.Format("[Номер налогового документа:{0}]", payDoc.Field("Номер налогового документа")));
                builder.Append(string.Format("[Дата налогового документа:{0}]", payDoc.Field("Дата налогового документа")));
                builder.Append(string.Format("[Тип налогового платежа:{0}]", payDoc.Field("Тип налогового платежа")));

                object[] items = payDoc.CashSymbols as object[];
                if (items != null) {
                    foreach (object item in items) {
                        object[] cashSymbol = (object[])item;
                        builder.Append(string.Format("[Кассовый символ:{0}#{1}]", (string)cashSymbol[0], (long)((decimal)cashSymbol[1] * 100)));
                    }
                }
            }

            if (procVersion == 1) {
                if (payDoc.KindDoc == 2 && razdel == 0) // 2	Платежное требование
                {
                    builder.Append(string.Format("[Дата акцепта:{0}]", ((DateTime)(payDoc.Field("Дата акцепта") ?? new DateTime(2222, 1, 1))).ToString("dd.MM.yyyy")));
                    builder.Append(string.Format("[Условие оплаты:{0}]", payDoc.ConditionPay));
                    builder.Append(string.Format("[Срок для акцепта:{0}]", payDoc.TermAccept));
                }

                if (payDoc.KindDoc == 16 && razdel == 0) // 16	Платежный ордер
                {
                    builder.Append(string.Format("[Номер частичной оплаты:{0}]", payDoc.Field("№ ч. плат.")));
                    builder.Append(string.Format("[Шифр платежного документа:{0}]", payDoc.Field("Шифр плат.док.")));
                    builder.Append(string.Format("[Номер платежного документа:{0}]", payDoc.Field("№ плат.док.")));
                    builder.Append(string.Format("[Дата платежного документа:{0}]", ((DateTime)(payDoc.Field("Дата плат.док.") ?? new DateTime(2222, 1, 1))).ToString("dd.MM.yyyy")));
                    builder.Append(string.Format("[Сумма остатка платежа:{0}]", (long)(((decimal)payDoc.Field("Сумма ост.пл.")) * 100)));
                    builder.Append(string.Format("[Содержание операции:{0}]", payDoc.Field("Содержание операции")));
                }

                if (payDoc.KindDoc == 16 && razdel == 0) // 8	Аккредитив
                {
                    builder.Append(string.Format("[N сч. получателя:{0}]", payDoc.Field("N сч. получателя")));
                    builder.Append(string.Format("[Вид аккредитива:{0}]", payDoc.Field("Вид аккредитива")));
                    builder.Append(string.Format("[Платеж по представлению:{0}]", payDoc.Field("Платеж по представлению")));
                    builder.Append(string.Format("[Дополнительные условия:{0}]", payDoc.Field("Дополнительные условия")));
                }
            }
            //System.IO.File.AppendAllText(@"\\SATURN\UBS_NT\Files\digsig.log.txt", "ID " + payDoc.Id.ToString() + "\r\n" + builder.ToString() +"\r\n\r\n" );
            //System.IO.File.AppendAllText(@"C:\001\digsig.log.txt", "ID " + payDoc.Id.ToString() + "\r\n" + builder.ToString() + "\r\n\r\n");
            return builder.ToString();
        }
        private static string GetDataTableOrderForSigning(IUbsDbConnection iUbsDb, IUbsWss iUbsWss, int idOrder, byte razdel, byte procVersion, out byte ssdKinddok) {
            ssdKinddok = 0;

            iUbsDb.ClearParameters();
            iUbsDb.CmdText =
                "select ID_ORDER, NUM_ORDER, DATE_ORDER, TYPE_ORDER, SIDE_ACC, ID_ACCOUNT" +
                    ", COUNT_DOC, COUNT_SHEET, OCHER_PAYM, MEANING, NUM_DIVISION" +
                " from " + "OD_TABLE_ORDER_" + razdel.ToString() +
                " where ID_ORDER = @ID_ORDER";
            iUbsDb.AddInputParameter("ID_ORDER", System.Data.SqlDbType.Int, idOrder);

            object[][] records = iUbsDb.ExecuteReadAllRec2();
            if (records == null) return null;

            // Меняем типы для табличного ордера
            ssdKinddok = (byte)((byte)records[0][3] + 100);

            StringBuilder builder = new StringBuilder();

            builder.Append(string.Format("[Идентификатор ордера:{0}]", (int)records[0][0]));
            builder.Append(string.Format("[Номер ордера:{0}]", (string)records[0][1]));
            builder.Append(string.Format("[Дата ордера:{0}]", ((DateTime)records[0][2]).ToString("dd.MM.yyyy")));
            builder.Append(string.Format("[Тип ордера:{0}]", (byte)records[0][3]));
            builder.Append(string.Format("[Сторона общего счета:{0}]", (byte)records[0][4]));

            builder.Append(string.Format("[Идентификатор общего счета:{0}]", (int)records[0][5]));
            builder.Append(string.Format("[Количество документов:{0}]", (int)records[0][6]));
            builder.Append(string.Format("[Количество листов:{0}]", (int)records[0][7]));

            builder.Append(string.Format("[Очередность платежа:{0}]", (byte)records[0][8]));
            builder.Append(string.Format("[Назначение платежа:{0}]", (string)records[0][9]));
            builder.Append(string.Format("[Номер отделения:{0}]", (short)records[0][10]));

            iUbsDb.ClearParameters();
            iUbsDb.CmdText =
                "select ID_DOC from " + (razdel == 0 ? "ARC_GLOBAL_" : "") + "OD_TABLE_ORDER_<RAZDEL>_DOC where ID_ORDER = @ID_ORDER".Replace("<RAZDEL>", razdel.ToString()) +
                (procVersion < 5 ? "" : " order by ID_DOC asc");
            iUbsDb.AddInputParameter("ID_ORDER", System.Data.SqlDbType.Int, idOrder);

            records = iUbsDb.ExecuteReadAllRec2();
            if (records != null) {
                byte emptyParameter;
                string emptyParameter2;
                for (int i = 0; i < records.GetUpperBound(0); i++)
                    builder.Append(GetDataDocumentForSigning(iUbsDb, iUbsWss, (int)records[i][0], razdel, procVersion, out emptyParameter, out emptyParameter2));
            }

            return builder.ToString();
        }

        #endregion

        /// <summary>
        /// Возвращает заблокированную сумму по пассивному счету в валюте счета
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="idAccount">Идентифйикатор счета</param>
        /// <param name="dateEnd">Дата окончания</param>
        /// <param name="isArrestAccount">Признак ареста счета ИФНС</param>
        /// <param name="blockedAmmontTax">Сумма приостановлений ИФНС в валюте счета</param>
        /// <param name="blockedAmmontBS">Сумма приостановлений ССП в валюте счета</param>
        /// <param name="blockedAmmontFTS">Сумма приостановлений ФТС в валюте счета</param>
        /// <returns>Заблокированная сумма по счету</returns>
        private static decimal GetBlockedAmountAccount(IUbsDbConnection connection, IUbsWss ubs, int idAccount, DateTime dateEnd, out bool isArrestAccount, out decimal blockedAmmontTax, out decimal blockedAmmontBS, out decimal blockedAmmontFTS) {
            isArrestAccount = false;
            decimal arrestAmmont = blockedAmmontTax = blockedAmmontBS = blockedAmmontFTS = 0;

            dateEnd = dateEnd.Date;
            DateTime dateNext = dateEnd.AddDays(1);

            connection.ClearParameters();
            connection.CmdText =
                "select b.TYPE_SOURCE, round(b.SUMMA / r.RATE, 2), s.SALDO" +
                " from OD_ACCOUNTS0_BLOK_SUM b, OD_ACCOUNTS0 a, COM_RATES_CB r, OD_SALTRN0 s" +
                " where b.DATE_SET <= @DATE_END" +
                    " and b.DATE_END > @DATE_END" +
                    " and a.ID_ACCOUNT = b.ID_ACCOUNT" +
                    " and s.ID_ACCOUNT = b.ID_ACCOUNT" +
                    " and b.ID_ACCOUNT = @ID_ACCOUNT" +
                    " and r.ID_CURRENCY = a.ID_CURRENCY" +
                    " and r.DATE_RATE <= @DATE_END" +
                    " and r.DATE_NEXT > @DATE_END" +
                    " and s.DATE_TRN <= @DATE_END" +
                    " and s.DATE_NEXT > @DATE_NEXT" +
                " order by b.TYPE_SOURCE desc";

            connection.AddInputParameter("DATE_END", System.Data.SqlDbType.DateTime, dateEnd);
            connection.AddInputParameter("DATE_NEXT", System.Data.SqlDbType.DateTime, dateNext);
            connection.AddInputParameter("ID_ACCOUNT", System.Data.SqlDbType.Int, idAccount);

            connection.ExecuteUbsDbReader();
            while (connection.Read()) {
                byte type = connection.GetByte(0);
                decimal sum = connection.GetDecimal(1);
                if (sum == 0) {
                    isArrestAccount = true;
                    if (arrestAmmont == 0) { arrestAmmont += connection.GetDecimal(2); }
                }
                else {
                    switch (type) {
                        case 1: blockedAmmontBS += sum; break; // ССП
                        case 2: blockedAmmontTax += sum; break; // ИФНС
                        case 3: blockedAmmontFTS += sum; break; // ФТС
                    }
                }
            }
            connection.CloseReader();

            // в случае ареста arrestAmmont должен быть больше 0 для пассивных счетов, на активные счета вообще устанавливаться не должен
            // если сумма ареста все же отрицательна, то средств на счете нет изначально.

            return blockedAmmontTax + blockedAmmontBS + blockedAmmontFTS + (arrestAmmont < 0 ? 0 : arrestAmmont);
        }
        /// <summary>
        /// Получить суммы документов стоящих на картотеке 1 и 2
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="idAccount">Идентифйикатор счета</param>
        /// <param name="sum1">Сумма картотеки 1</param>
        /// <param name="sum2">Сумма картотеки 2</param>
        public static void GetSumCardIndex(IUbsDbConnection connection, IUbsWss ubs, int idAccount, out decimal sum1, out decimal sum2) {
            sum1 = sum2 = 0;

            connection.ClearParameters();
            connection.CmdText = "select SUM1, SUM2 from OD_DOC_0_CI_ACC where ID_ACCOUNT = @ID_ACCOUNT";
            connection.AddInputParameter("ID_ACCOUNT", idAccount);
            object[] record = connection.ExecuteReadFirstRec();
            if (record == null) return;

            if (Convert.ToDecimal(record[0]) > 0) sum1 = Convert.ToDecimal(record[0]);
            if (Convert.ToDecimal(record[1]) > 0) sum2 = Convert.ToDecimal(record[1]);
        }
        /// <summary>
        /// Получить суммы документов стоящих на картотеке 1 и 2
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="straccount">Номер счета</param>
        /// <param name="sum1">Сумма картотеки 1</param>
        /// <param name="sum2">Сумма картотеки 2</param>
        public static void GetSumCardIndex(IUbsDbConnection connection, IUbsWss ubs, string straccount, out decimal sum1, out decimal sum2) {
            connection.ClearParameters();
            connection.CmdText = "select ID_ACCOUNT from OD_ACCOUNTS0 where STRACCOUNT = '" + straccount + "'";
            int accountId = Convert.ToInt32(connection.ExecuteScalar());

            GetSumCardIndex(connection, ubs, accountId, out sum1, out sum2);
        }

        /// <summary>
        /// Получить суммы документов стоящих на картотеке 1 и 2
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="idAccount">Идентифйикатор счета</param>
        /// <param name="excludesIdDocuments">Массив исключаемых документов</param>
        /// <param name="sum1Db">Сумма картотеки 1 Дб</param>
        /// <param name="sum1Cr">Сумма картотеки 1 Кр</param>
        /// <param name="sum2Db">Сумма картотеки 2 Дб</param>
        /// <param name="sum2Cr">Сумма картотеки 2 Кр</param>
        private static void GetSumCardIndex(IUbsDbConnection connection, IUbsWss ubs, int idAccount, /*DateTime dateEnd,*/ int[] excludesIdDocuments, out decimal sum1Db, out decimal sum1Cr, out decimal sum2Db, out decimal sum2Cr) {
            sum1Db = sum1Cr = sum2Db = sum2Cr = 0;
            if (idAccount == 0) return;

            DateTime dateStart = Convert.ToDateTime(ubs.UbsWssParam("CommonDate", "Закрытый операционный день")).AddDays(1);
            //dateEnd = dateEnd.Date.AddDays(1);

            List<object> listExcludesDocuments = new List<object>();
            if (excludesIdDocuments != null && excludesIdDocuments.Length > 0)
                foreach (int idDocument in excludesIdDocuments) listExcludesDocuments.Add(idDocument);

            string query =
                "select dar.DB_CR, doc.DATE_TRN" +
                    ", doc.SET_OBOROT_DB, doc.OBOROT_DB, doc.ID_CURRENCY_DB" +
                    ", doc.SET_OBOROT_CR, doc.OBOROT_CR, doc.ID_CURRENCY_CR" +
                    ", ci.CARD_INDEX" +
                " from OD_DOC_0 doc, OD_DOC_0_DAR dar, OD_DOC_0_CARD_INDEX ci" +
                " where doc.ID_DOC = dar.ID_DOC" +
                    " and dar.ID_ACCOUNT = @ID_ACCOUNT" +
                    (listExcludesDocuments.Count > 0 ? " and dar.ID_DOC not in (@ID_DOC_IN)" : "") +
                    " and doc.EXEC_TRN = 0" +
                    " and dar.DATE_TRN >= @DATE_START" +
                    " and ci.ID_DOC = doc.ID_DOC";

            connection.ClearParameters();
            connection.CmdText = query;
            connection.AddInputParameter("ID_ACCOUNT", System.Data.SqlDbType.Int, idAccount);
            connection.AddInputParameter("DATE_START", System.Data.SqlDbType.DateTime, dateStart);
            if (listExcludesDocuments.Count > 0) connection.AddInputParameterIN("ID_DOC_IN", System.Data.SqlDbType.Int, listExcludesDocuments.ToArray());
            

            object[][] records = connection.ExecuteReadAllRec2();
            if (records == null) return;

            decimal rateDb, nuDb, rateCr, nuCr;
            for (int i = 0; i <= records.GetUpperBound(0); i++) {
                byte dbcr = Convert.ToByte(records[i][0]);
                DateTime dateTrn = Convert.ToDateTime(records[i][1]);

                bool setOborotDb = Convert.ToBoolean(records[i][2]);
                decimal oborotDb = Convert.ToDecimal(records[i][3]);
                short idCurrencyDb = Convert.ToInt16(records[i][4]);

                bool setOborotCr = Convert.ToBoolean(records[i][5]);
                decimal oborotCr = Convert.ToDecimal(records[i][6]);
                short idCurrencyCr = Convert.ToInt16(records[i][7]);
                byte cardIndex = Convert.ToByte(records[i][8]);

                if (idCurrencyDb == 0) idCurrencyDb = idCurrencyCr;
                if (idCurrencyCr == 0) idCurrencyCr = idCurrencyDb;

                if (dbcr == 0) {
                    if (!setOborotDb && setOborotCr) {
                        if (idCurrencyDb == idCurrencyCr)
                            oborotDb = oborotCr;
                        else {
                            GetRate(connection, ubs, dateTrn, idCurrencyDb, idCurrencyCr, out rateDb, out nuDb, out rateCr, out nuCr);
                            oborotDb = Math.Round(oborotCr * (rateCr / nuCr) / (rateDb / nuDb), 2);
                        }
                    }

                    if (cardIndex == 1) sum1Db += oborotDb; else sum2Db += oborotDb;
                }
                else {
                    if (!setOborotCr && setOborotDb) {
                        if (idCurrencyDb == idCurrencyCr)
                            oborotCr = oborotDb;
                        else {
                            GetRate(connection, ubs, dateTrn, idCurrencyDb, idCurrencyCr, out rateDb, out nuDb, out rateCr, out nuCr);
                            oborotCr = Math.Round(oborotDb * (rateDb / nuDb) / (rateCr / nuCr), 2);
                        }
                    }
                    if (cardIndex == 1) sum1Cr += oborotCr; else sum2Cr += oborotCr;
                }
            }
        }
        /// <summary>
        /// Получить суммы документов стоящих на валютной картотеке 2
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="idAccount">Идентифйикатор счета</param>
        /// <param name="dateEnd">Дата окночания</param>
        /// <param name="excludesIdDocuments">Массив идентификаторов документов исключаемых из поиска</param>
        /// <param name="sumCurrency2">Сумма валютной картотеки 2</param>
        /// <param name="sumCurrency2InCurrency">Сумма валютной картотеки 2 в валюте счета</param>
        private static void GetSumCardIndexCurrency(IUbsDbConnection connection, IUbsWss ubs, int idAccount, DateTime dateEnd, int[] excludesIdDocuments, out decimal sumCurrency2, out decimal sumCurrency2InCurrency) {
            sumCurrency2 = 0;
            sumCurrency2InCurrency = 0;

            List<object> listExcludesDocuments = new List<object>();
            if (excludesIdDocuments != null && excludesIdDocuments.Length > 0)
                foreach (int idDocument in excludesIdDocuments) listExcludesDocuments.Add(idDocument);

            connection.ClearParameters();
            connection.CmdText =
                "select isnull(sum(isnull(d.REST, 0)), 0), isnull(sum(round(isnull(d.REST, 0) / r.RATE, 2)), 0)" +
                " from OD_REQ_TO_CURR_ACC d, OD_ACCOUNTS0 a, COM_RATES_CB r, OD_REQTO_CACC_ADDFL_DIC dic" +
                " where d.ID_ACCOUNT = @ID_ACCOUNT" +
                    " and a.ID_ACCOUNT = d.ID_ACCOUNT" +
                    " and r.ID_CURRENCY = a.ID_CURRENCY" +
                    " and r.DATE_RATE <= d.DATE_PAYM" +
                    " and r.DATE_NEXT > d.DATE_PAYM" +
                    (listExcludesDocuments.Count > 0 ? " and d.ID_DOC not in (@ID_DOC_IN)" : "") +
                    " and dic.NAME_FIELD = 'Тип приостановления оплаты'" +
                    " and not exists(select * from OD_REQTO_CACC_ADDFL_INT ad where ad.ID_FIELD = dic.ID_FIELD and ad.ID_OBJECT = d.ID_DOC and ad.FIELD > 0)";
            
            connection.AddInputParameter("ID_ACCOUNT", System.Data.SqlDbType.Int, idAccount);
            //connection.AddInputParameter("DATE_END", System.Data.SqlDbType.DateTime, dateEnd);
            if (listExcludesDocuments.Count > 0) connection.AddInputParameterIN("ID_DOC_IN", System.Data.SqlDbType.Int, listExcludesDocuments.ToArray());

            object[] record = connection.ExecuteReadFirstRec();
            if (record == null) return;

            sumCurrency2 = Convert.ToDecimal(record[0]);
            sumCurrency2InCurrency = Convert.ToDecimal(record[1]);
        }
        /// <summary>
        /// Получить плановые обороты по счету раздела А
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="idAccount">Идентификатор счета</param>
        /// <param name="dateEnd">Дата окончания</param>
        /// <param name="excludesIdDocuments">Массив исключаемых документов</param>
        /// <param name="includesKindDocs">Массив требуемых видов документов</param>
        /// <param name="totalOborotDb">Сумарный дебетовый оборот</param>
        /// <param name="totalOborotCr">Сумарный кредитовый оборот</param>
        /// <returns>Плановый оборот = (Плановый кредитовый оборот - Плановый дебетовый оборот)</returns>
        private static decimal GetPlanOborotA(IUbsDbConnection connection, IUbsWss ubs, int idAccount, DateTime dateEnd, int[] excludesIdDocuments, byte[] includesKindDocs, out decimal totalOborotDb, out decimal totalOborotCr) {
            totalOborotDb = totalOborotCr = 0;
            if (idAccount == 0) return 0;


            DateTime dateStart = Convert.ToDateTime(ubs.UbsWssParam("CommonDate", "Закрытый операционный день")).AddDays(1);
            dateEnd = dateEnd.Date.AddDays(1);

            List<object> listExcludesDocuments = new List<object>();
            if (excludesIdDocuments != null && excludesIdDocuments.Length > 0)
                foreach (int idDocument in excludesIdDocuments) listExcludesDocuments.Add(idDocument);

            List<object> listIncludesKindDocs = new List<object>();
            if (includesKindDocs != null && includesKindDocs.Length > 0)
                foreach (byte kinddoc in includesKindDocs) listIncludesKindDocs.Add(kinddoc);

            string query =
                "select dar.DB_CR, doc.DATE_TRN" +
                    ", doc.SET_OBOROT_DB, doc.OBOROT_DB, doc.ID_CURRENCY_DB" +
                    ", doc.SET_OBOROT_CR, doc.OBOROT_CR, doc.ID_CURRENCY_CR" +
                " from OD_DOC_0 doc, OD_DOC_0_DAR dar, OD_DOC_0_ADDFL_DIC dic" +
                " where doc.ID_DOC = dar.ID_DOC" +
                    " and dar.ID_ACCOUNT = @ID_ACCOUNT" +
                    (listExcludesDocuments.Count > 0 ? " and dar.ID_DOC not in (@ID_DOC_IN)" : "") +
                    " and doc.EXEC_TRN = 0" +
                    " and dar.DATE_TRN >= @DATE_START" +
                    " and dar.DATE_TRN < @DATE_END" +
                    (listIncludesKindDocs.Count > 0 ? " and doc.KINDDOC in (@KINDDOC_IN)" : "") +
                    " and dic.NAME_FIELD = 'Тип приостановления оплаты'" +
                    " and not exists (select * from OD_DOC_0_ADDFL_INT ad where ad.ID_FIELD = dic.ID_FIELD and ad.ID_OBJECT = doc.ID_DOC and ad.FIELD > 0)";

            connection.ClearParameters();
            connection.CmdText = query;
            connection.AddInputParameter("ID_ACCOUNT", System.Data.SqlDbType.Int, idAccount);
            connection.AddInputParameter("DATE_START", System.Data.SqlDbType.DateTime, dateStart);
            connection.AddInputParameter("DATE_END", System.Data.SqlDbType.DateTime, dateEnd);
            if (listExcludesDocuments.Count > 0) connection.AddInputParameterIN("ID_DOC_IN", System.Data.SqlDbType.Int, listExcludesDocuments.ToArray());
            if (listIncludesKindDocs.Count > 0) connection.AddInputParameterIN("KINDDOC_IN", System.Data.SqlDbType.TinyInt, listIncludesKindDocs.ToArray());

            object[][] records = connection.ExecuteReadAllRec2();
            if (records == null) return 0;

            decimal rateDb, nuDb, rateCr, nuCr;
            for (int i = 0; i <= records.GetUpperBound(0); i++) {
                byte dbcr = Convert.ToByte(records[i][0]);
                DateTime dateTrn = Convert.ToDateTime(records[i][1]);

                bool setOborotDb = Convert.ToBoolean(records[i][2]);
                decimal oborotDb = Convert.ToDecimal(records[i][3]);
                short idCurrencyDb = Convert.ToInt16(records[i][4]);

                bool setOborotCr = Convert.ToBoolean(records[i][5]);
                decimal oborotCr = Convert.ToDecimal(records[i][6]);
                short idCurrencyCr = Convert.ToInt16(records[i][7]);

                if (idCurrencyDb == 0) idCurrencyDb = idCurrencyCr;
                if (idCurrencyCr == 0) idCurrencyCr = idCurrencyDb;

                if (dbcr == 0) {
                    if (!setOborotDb && setOborotCr) {
                        if (idCurrencyDb == idCurrencyCr)
                            oborotDb = oborotCr;
                        else {
                            GetRate(connection, ubs, dateTrn, idCurrencyDb, idCurrencyCr, out rateDb, out nuDb, out rateCr, out nuCr);
                            oborotDb = Math.Round(oborotCr * (rateCr / nuCr) / (rateDb / nuDb), 2);
                        }
                    }
                    totalOborotDb += oborotDb;
                }
                else {
                    if (!setOborotCr && setOborotDb) {
                        if (idCurrencyDb == idCurrencyCr)
                            oborotCr = oborotDb;
                        else {
                            GetRate(connection, ubs, dateTrn, idCurrencyDb, idCurrencyCr, out rateDb, out nuDb, out rateCr, out nuCr);
                            oborotCr = Math.Round(oborotDb * (rateDb / nuDb) / (rateCr / nuCr), 2);
                        }
                    }
                    totalOborotCr += oborotCr;
                }
            }

            return totalOborotCr - totalOborotDb;
        }
        /// <summary>
        /// Получить плановые обороты по счету раздела А для валютных требований
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="idAccount">Идентификатор счета</param>
        /// <param name="dateEnd">Дата окончания</param>
        /// <param name="excludesIdDocuments">Массив исключаемых документов</param>
        /// <param name="totalOborotDb">Сумарный дебетовый оборот</param>
        /// <param name="totalOborotCr">Сумарный кредитовый оборот</param>
        /// <returns>Плановый оборот = (Плановый кредитовый оборот - Плановый дебетовый оборот)</returns>
        private static decimal GetPlanOborotCurrency(IUbsDbConnection connection, IUbsWss ubs, int idAccount, DateTime dateEnd, int[] excludesIdDocuments, out decimal totalOborotDb, out decimal totalOborotCr) {
            totalOborotDb = totalOborotCr = 0;
            if (idAccount == 0) return 0;

            DateTime dateStart = Convert.ToDateTime(ubs.UbsWssParam("CommonDate", "Закрытый операционный день")).AddDays(1);
            dateEnd = dateEnd.Date.AddDays(1);

            List<object> listExcludesDocuments = new List<object>();
            if (excludesIdDocuments != null && excludesIdDocuments.Length > 0)
                foreach (int idDocument in excludesIdDocuments) listExcludesDocuments.Add(idDocument);


            string query =
                "select 0, r.DATE_PAYM, r.SUMMA, a.ID_CURRENCY" +
                " from OD_REQ_TO_CURR_ACC r" +
                    " inner join OD_ACCOUNTS0 a on a.STRACCOUNT = r.STRACCOUNT_P and a.ID_ACCOUNT = @ID_ACCOUNT" +
                        (listExcludesDocuments.Count > 0 ? " and r.ID_DOC not in (@ID_DOC_IN)" : "") +
                        " and r.SUMMA > 0" +
                        " and r.DATE_PAYM >= @DATE_START" +
                        " and r.DATE_PAYM < @DATE_END" +
                    " inner join OD_REQTO_CACC_ADDFL_DIC dic on dic.NAME_FIELD = 'Тип приостановления оплаты'" +
                        " and not exists(select * from OD_REQTO_CACC_ADDFL_INT ad where ad.ID_FIELD = dic.ID_FIELD and ad.ID_OBJECT = r.ID_DOC and ad.FIELD > 0)" +
                " union all" +
                " select 1, r.DATE_PAYM, r.SUMMA, a.ID_CURRENCY" +
                " from OD_REQ_TO_CURR_ACC r" +
                    " inner join OD_ACCOUNTS0 a on a.STRACCOUNT = r.STRACCOUNT_R and a.ID_ACCOUNT = @ID_ACCOUNT" +
                        (listExcludesDocuments.Count > 0 ? " and r.ID_DOC not in (@ID_DOC_IN)" : "") +
                        " and r.SUMMA > 0" +
                        " and r.DATE_PAYM >= @DATE_START" +
                        " and r.DATE_PAYM < @DATE_END" +
                    " inner join OD_REQTO_CACC_ADDFL_DIC dic on dic.NAME_FIELD = 'Тип приостановления оплаты'" +
                        " and not exists(select * from OD_REQTO_CACC_ADDFL_INT ad where ad.ID_FIELD = dic.ID_FIELD and ad.ID_OBJECT = r.ID_DOC and ad.FIELD > 0)";

            connection.ClearParameters();
            connection.CmdText = query;
            connection.AddInputParameter("ID_ACCOUNT", System.Data.SqlDbType.Int, idAccount);
            connection.AddInputParameter("DATE_START", System.Data.SqlDbType.DateTime, dateStart);
            connection.AddInputParameter("DATE_END", System.Data.SqlDbType.DateTime, dateEnd);
            if (listExcludesDocuments.Count > 0) connection.AddInputParameterIN("ID_DOC_IN", System.Data.SqlDbType.Int, listExcludesDocuments.ToArray());
           
            object[][] records = connection.ExecuteReadAllRec2();
            if (records == null) return 0;

            decimal rateDb, nuDb, rateCr, nuCr;
            for (int i = 0; i <= records.GetUpperBound(0); i++) {
                byte dbcr = Convert.ToByte(records[i][0]);
                DateTime dateTrn = Convert.ToDateTime(records[i][1]);

                decimal oborot = Convert.ToDecimal(records[i][2]);
                short idCurrency = Convert.ToInt16(records[i][3]);

                GetRate(connection, ubs, dateTrn, idCurrency, 810, out rateDb, out nuDb, out rateCr, out nuCr);
                oborot = Math.Round(oborot * (rateCr / nuCr) / (rateDb / nuDb), 2);

                if (dbcr == 0)
                    totalOborotDb += oborot;
                else
                    totalOborotCr += oborot;
            }

            return totalOborotCr - totalOborotDb;
        }
        /// <summary>
        /// Получить плановые обороты
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="razdel">Номер раздела 0 - А, 1 - Б, 2 - B, 3 - Г, 4 - Д</param>
        /// <param name="idAccount">Идентификатор счета</param>
        /// <param name="dateEnd">Дата конца интервала</param>
        /// <param name="excludesIdDocuments">Массив идентификаторов документов исключаемых из поиска</param>
        /// <param name="totalOborotDb">Плановый дебетовый оборот</param>
        /// <param name="totalOborotCr">Плановый кредитовый оборот</param>
        /// <returns>Плановый оборот = (Плановый кредитовый оборот - Плановый дебетовый оборот)</returns>
        private static decimal GetPlanOborot(IUbsDbConnection connection, IUbsWss ubs, byte razdel, int idAccount, DateTime dateEnd, int[] excludesIdDocuments, out decimal totalOborotDb, out decimal totalOborotCr) {
            totalOborotDb = totalOborotCr = 0;
            if (idAccount == 0) return 0;

            DateTime dateStart = Convert.ToDateTime(ubs.UbsWssParam("CommonDate", "Закрытый операционный день")).AddDays(1);
            dateEnd = dateEnd.Date.AddDays(1);

            List<object> listExcludesDocuments = new List<object>();
            if (excludesIdDocuments != null && excludesIdDocuments.Length > 0)
                foreach (int idDocument in excludesIdDocuments) listExcludesDocuments.Add(idDocument);

            connection.ClearParameters();
            connection.CmdText =
                "select {fn ifnull(sum(d.OBOROT" + (razdel == 4 ? "" : "_DB") + "), 0)}" +
                " from OD_DOC_" + razdel.ToString() + " d, OD_DOC_" + razdel.ToString() + "_DAR dar" +
                " where d.EXEC_TRN = 0" +
                    " and d.ID_DOC = dar.ID_DOC" +
                    " and dar.ID_ACCOUNT = @ID_ACCOUNT" +
                    " and dar.DATE_TRN >= @DATE_START" +
                    " and dar.DATE_TRN < @DATE_END" +
                    " and dar.DB_CR = 0" +
                    (listExcludesDocuments.Count > 0 ? " and dar.ID_DOC not in (@ID_DOC_IN)" : "");

            connection.AddInputParameter("ID_ACCOUNT", System.Data.SqlDbType.Int, idAccount);
            connection.AddInputParameter("DATE_START", System.Data.SqlDbType.DateTime, dateStart);
            connection.AddInputParameter("DATE_END", System.Data.SqlDbType.DateTime, dateEnd);
            if (listExcludesDocuments.Count > 0) connection.AddInputParameterIN("ID_DOC_IN", System.Data.SqlDbType.Int, listExcludesDocuments.ToArray());
            object scalar = connection.ExecuteScalar();
            connection.CmdReset();

            if (scalar != DBNull.Value && scalar != null) totalOborotDb = Convert.ToDecimal(scalar);


            connection.ClearParameters();
            connection.CmdText =
                "select {fn ifnull(sum(d.OBOROT" + (razdel == 4 ? "" : "_CR") + "), 0)}" +
                " from OD_DOC_" + razdel.ToString() + " d, OD_DOC_" + razdel.ToString() + "_DAR dar" +
                " where d.EXEC_TRN = 0" +
                    " and d.ID_DOC = dar.ID_DOC" +
                    " and dar.ID_ACCOUNT = @ID_ACCOUNT" +
                    " and dar.DATE_TRN >= @DATE_START" +
                    " and dar.DATE_TRN < @DATE_END" +
                    " and dar.DB_CR = 1" +
                    (listExcludesDocuments.Count > 0 ? " and dar.ID_DOC not in (@ID_DOC_IN)" : "");

            connection.AddInputParameter("ID_ACCOUNT", System.Data.SqlDbType.Int, idAccount);
            connection.AddInputParameter("DATE_START", System.Data.SqlDbType.DateTime, dateStart);
            connection.AddInputParameter("DATE_END", System.Data.SqlDbType.DateTime, dateEnd);
            if (listExcludesDocuments.Count > 0) connection.AddInputParameterIN("ID_DOC_IN", System.Data.SqlDbType.Int, listExcludesDocuments.ToArray());
            scalar = connection.ExecuteScalar();
            connection.CmdReset();

            if (scalar != DBNull.Value && scalar != null) totalOborotCr = Convert.ToDecimal(scalar);

            return totalOborotCr - totalOborotDb;
        }
        /// <summary>
        /// Получить суммы комиссий за РКО
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="idAccount">Идентификатор счета</param>
        /// <param name="dateEnd">Дата конца интервала</param>
        /// <param name="excludesIdDocuments">Массив идентификаторов документов исключаемых из поиска</param>
        /// <param name="contractId">Идентификатор договора РКО</param>
        /// <param name="sumCommission">Сумма комиссий</param>
        private static void GetSumCommissionRKO(IUbsDbConnection connection, IUbsWss ubs, int idAccount, DateTime dateEnd, int[] excludesIdDocuments, out int contractId, out decimal sumCommission) {
            contractId = 0;
            sumCommission = 0;

            DateTime dateStart = Convert.ToDateTime(ubs.UbsWssParam("CommonDate", "Закрытый операционный день")).AddDays(1);
            dateEnd = dateEnd.Date.AddDays(1);

            List<object> listExcludesDocuments = new List<object>();
            if (excludesIdDocuments != null && excludesIdDocuments.Length > 0)
                foreach (int idDocument in excludesIdDocuments) listExcludesDocuments.Add(idDocument);

            DateTime dateSettlement = dt22220101;

            connection.CmdText = "select ID_BUSINESS from UBS_BUSINESS where COD_BUSINESS = 'RKO'";
            if (Convert.ToInt32(connection.ExecuteScalar()) == 0) return;


            connection.ClearParameters();
            connection.CmdText = 
                "select c.ID_CONTRACT, d.DATE_RO" +
                " from RKO_CONTRACT c, RKO_CONTRACT_DATE d" +
                " where c.ID_ACCOUNT = @ID_ACCOUNT" +
                    " and c.DATE_OPEN < @DATE_END and c.DATE_CLOSE >= @DATE_END" +
                    " and c.ID_CONTRACT = d.ID_CONTRACT";
            connection.AddInputParameter("ID_ACCOUNT", System.Data.SqlDbType.Int, idAccount);
            connection.AddInputParameter("DATE_END", System.Data.SqlDbType.DateTime, dateEnd);
            

            object[] record = connection.ExecuteReadFirstRec();
            if (record != null) {
                contractId = Convert.ToInt32(record[0]);
                dateSettlement = Convert.ToDateTime(record[1]).Date.AddDays(1);
            }
            if (contractId == 0) return;

            // По непроведенным документам и проведенным с датой проведения большей даты, по которую взята переодическая комиссия за расчетное обслуживание
            connection.ClearParameters();
            connection.CmdText = @"
                select isnull(sum(m.FIELD), 0)
                from OD_DOC_0_ADDFL_MONEY m
	                inner join OD_DOC_0_ADDFL_DIC ad on ad.NAME_FIELD = 'Сумма комиссии' and ad.ID_FIELD = m.ID_FIELD
	                inner join OD_DOC_0 d on d.ID_DOC = m.ID_OBJECT
	                    and d.DATE_TRN >= @DATE_START
	                    and d.DATE_TRN < @DATE_END
	                    and d.ID_ACCOUNT_DB = @ID_ACCOUNT
	                    and (d.EXEC_TRN = 0 or d.EXEC_TRN <> 0 and d.DATE_TRN >= @DATE_SETTLEMENT)" +
                        (listExcludesDocuments.Count > 0 ? " and d.ID_DOC not in (@ID_DOC_IN)" : "");
            connection.AddInputParameter("ID_ACCOUNT", System.Data.SqlDbType.Int, idAccount);
            connection.AddInputParameter("DATE_START", System.Data.SqlDbType.DateTime, dateStart);
            connection.AddInputParameter("DATE_END", System.Data.SqlDbType.DateTime, dateEnd);
            connection.AddInputParameter("DATE_SETTLEMENT", System.Data.SqlDbType.DateTime, dateSettlement);
            if (listExcludesDocuments.Count > 0) connection.AddInputParameterIN("ID_DOC_IN", System.Data.SqlDbType.Int, listExcludesDocuments.ToArray());
            object scalar = connection.ExecuteScalar();
            connection.CmdReset();

            if (scalar != DBNull.Value && scalar != null) sumCommission = Convert.ToDecimal(scalar);
        }

        /// <summary>
        /// Получить сумарный оборот непроведенных документов по счету в сквитованных пачках
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="operationDate">Дата операции</param>
        /// <param name="accountId">Идентификатор счета</param>
        /// <param name="folderId">Идентификатор пачки для исключения</param>
        /// <returns>Сумарный оборот</returns>
        public static decimal GetOborotConfirmedFolder(IUbsDbConnection connection, IUbsWss ubs, DateTime operationDate, int accountId, int folderId) {
            decimal oborotDb, oborotCr;
            return GetOborotConfirmedFolder(connection, ubs, operationDate, accountId, folderId, out oborotDb, out oborotCr);
        }

        /// <summary>
        /// Получить сумарный оборот непроведенных документов по счету в сквитованных пачках
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="operationDate">Дата операции</param>
        /// <param name="accountId">Идентификатор счета</param>
        /// <param name="folderId">Идентификатор пачки для исключения</param>
        /// <param name="oborotDb">Дебетовый оборот</param>
        /// <param name="oborotCr">Кредитовый оборот</param>
        /// <returns>Сумарный оборот</returns>
        public static decimal GetOborotConfirmedFolder(IUbsDbConnection connection, IUbsWss ubs, DateTime operationDate, int accountId, int folderId, out decimal oborotDb, out decimal oborotCr) {
            DateTime closeBankDate = Convert.ToDateTime(ubs.UbsWssParam("CommonDate", "Закрытый операционный день"));

            connection.ClearParameters();
            connection.CmdText =
                "select isnull(sum(d.OBOROT_DB), 0)" +
                " from OD_DOC_0 d, OD_DOC_0_DAR dar, OD_DOC_0_FOLDER f, OD_TYPE_FOLDER0 tf" +
                " where d.EXEC_TRN = 0 and d.ID_DOC = dar.ID_DOC and d.ID_FOLDER = f.ID_FOLDER" +
                    " and f.STATE_FOLDER in (1,2) and f.TYPE_FOLDER = tf.TYPE_FOLDER and tf.SID_FOLDER in ('UBS_PASS_RC', 'UBS_INTERNAL')" +
                    " and dar.DATE_TRN <= " + connection.sqlDate(operationDate) +
                    " and dar.DATE_TRN > " + connection.sqlDate(closeBankDate) +
                    " and dar.ID_ACCOUNT = " + accountId +
                    " and f.ID_FOLDER <> " + folderId +
                    " and dar.DB_CR = 0";
            oborotDb = Convert.ToDecimal(connection.ExecuteScalar());

            connection.CmdText =
                "select isnull(sum(d.OBOROT_CR), 0)" +
                " from OD_DOC_0 d, OD_DOC_0_DAR dar, OD_DOC_0_FOLDER f, OD_TYPE_FOLDER0 tf" +
                " where d.EXEC_TRN = 0 and d.ID_DOC = dar.ID_DOC and d.ID_FOLDER = f.ID_FOLDER" +
                    " and f.STATE_FOLDER in (1, 2) and f.TYPE_FOLDER = tf.TYPE_FOLDER and tf.SID_FOLDER in ('UBS_PASS_RC', 'UBS_INTERNAL')" +
                    " and dar.DATE_TRN <= " + connection.sqlDate(operationDate) +
                    " and dar.DATE_TRN > " + connection.sqlDate(closeBankDate) +
                    " and dar.ID_ACCOUNT = " + accountId +
                    " and f.ID_FOLDER <> " + folderId +
                    " and dar.DB_CR = 1";
            oborotCr = Convert.ToDecimal(connection.ExecuteScalar());

            return oborotCr - oborotDb;
        }

        /// <summary>
        /// Получить плановые обороты
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="idAccount">Идентификатор счета</param>
        /// <param name="dateEnd">Дата окончания</param>
        /// <param name="excludesIdDocuments">Массив исключаемых документов</param>
        /// <param name="includesKindDocs">Массив требуемых видов документов</param>
        /// <param name="includeDocCard">Учесть картотечные документы</param>
        /// <param name="totalOborotDb">Сумарный дебетовый оборот</param>
        /// <param name="totalOborotCr">Сумарный кредитовый оборот</param>
        /// <returns>Плановый оборот = (Плановый кредитовый оборот - Плановый дебетовый оборот)</returns>
        [Obsolete("Метод устарел, следует использовать метод GetAccountPrognosis с параметрами учета прогнозируемого остатка AccountPrognosis", true)]
        private static decimal GetPlanOborotA(IUbsDbConnection connection, IUbsWss ubs, int idAccount, DateTime dateEnd, int[] excludesIdDocuments, byte[] includesKindDocs, bool includeDocCard, out decimal totalOborotDb, out decimal totalOborotCr) {
            totalOborotDb = totalOborotCr = 0;
            if (idAccount == 0) return 0;

            DateTime dateStart = Convert.ToDateTime(ubs.UbsWssParam("CommonDate", "Закрытый операционный день")).AddDays(1);
            dateEnd = dateEnd.Date.AddDays(1);

            List<object> listExcludesDocuments = new List<object>();
            if (excludesIdDocuments != null && excludesIdDocuments.Length > 0)
                foreach (int idDocument in excludesIdDocuments) listExcludesDocuments.Add(idDocument);

            List<object> listIncludesKindDocs = new List<object>();
            if (includesKindDocs != null && includesKindDocs.Length > 0)
                foreach (byte kinddoc in includesKindDocs) listIncludesKindDocs.Add(kinddoc);

            string query =
                "select dar.DB_CR, doc.DATE_TRN" +
                    ", doc.SET_OBOROT_DB, doc.OBOROT_DB, doc.ID_CURRENCY_DB" +
                    ", doc.SET_OBOROT_CR, doc.OBOROT_CR, doc.ID_CURRENCY_CR" +
                " from OD_DOC_0 doc, OD_DOC_0_DAR dar" +
                " where doc.ID_DOC = dar.ID_DOC" +
                    " and dar.ID_ACCOUNT = @ID_ACCOUNT" +
                    (listExcludesDocuments.Count > 0 ? " and dar.ID_DOC not in (@ID_DOC_IN)" : "") +
                    " and doc.EXEC_TRN = 0" +
                    " and dar.DATE_TRN >= @DATE_START";

            if (includeDocCard) {
                query +=
                    " and ( dar.DATE_TRN < @DATE_END" +
                    (listIncludesKindDocs.Count > 0 ? " and doc.KINDDOC in (@KINDDOC_IN)" : "") +
                    " or doc.SET_CART > 0 and exists(select * from OD_DOC_0_CARD_INDEX ci where ci.CARD_INDEX = 2 and ci.ID_DOC = dar.ID_DOC))";
            }
            else {
                query +=
                    " and dar.DATE_TRN < @DATE_END" +
                    (listIncludesKindDocs.Count > 0 ? " and doc.KINDDOC in (@KINDDOC_IN)" : "");
            }

            connection.ClearParameters();
            connection.CmdText = query;
            connection.AddInputParameter("ID_ACCOUNT", System.Data.SqlDbType.Int, idAccount);
            connection.AddInputParameter("DATE_START", System.Data.SqlDbType.DateTime, dateStart);
            connection.AddInputParameter("DATE_END", System.Data.SqlDbType.DateTime, dateEnd);
            if (listExcludesDocuments.Count > 0) connection.AddInputParameterIN("ID_DOC_IN", System.Data.SqlDbType.Int, listExcludesDocuments.ToArray());
            if (listIncludesKindDocs.Count > 0) connection.AddInputParameterIN("KINDDOC_IN", System.Data.SqlDbType.TinyInt, listIncludesKindDocs.ToArray());

            object[][] records = connection.ExecuteReadAllRec2();
            if (records == null) return 0;

            decimal rateDb, nuDb, rateCr, nuCr;
            for (int i = 0; i <= records.GetUpperBound(0); i++) {
                byte dbcr = Convert.ToByte(records[i][0]);
                DateTime dateTrn = Convert.ToDateTime(records[i][1]);

                bool setOborotDb = Convert.ToBoolean(records[i][2]);
                decimal oborotDb = Convert.ToDecimal(records[i][3]);
                short idCurrencyDb = Convert.ToInt16(records[i][4]);

                bool setOborotCr = Convert.ToBoolean(records[i][5]);
                decimal oborotCr = Convert.ToDecimal(records[i][6]);
                short idCurrencyCr = Convert.ToInt16(records[i][7]);

                if (idCurrencyDb == 0) idCurrencyDb = idCurrencyCr;
                if (idCurrencyCr == 0) idCurrencyCr = idCurrencyDb;

                if (dbcr == 0) {
                    if (!setOborotDb && setOborotCr) {
                        if (idCurrencyDb == idCurrencyCr)
                            oborotDb = oborotCr;
                        else {
                            GetRate(connection, ubs, dateTrn, idCurrencyDb, idCurrencyCr, out rateDb, out nuDb, out rateCr, out nuCr);
                            oborotDb = Math.Round(oborotCr * (rateCr / nuCr) / (rateDb / nuDb), 2);
                        }
                    }
                    totalOborotDb += oborotDb;
                }
                else {
                    if (!setOborotCr && setOborotDb) {
                        if (idCurrencyDb == idCurrencyCr)
                            oborotCr = oborotDb;
                        else {
                            GetRate(connection, ubs, dateTrn, idCurrencyDb, idCurrencyCr, out rateDb, out nuDb, out rateCr, out nuCr);
                            oborotCr = Math.Round(oborotDb * (rateDb / nuDb) / (rateCr / nuCr), 2);
                        }
                    }
                    totalOborotCr += oborotCr;
                }
            }

            return totalOborotCr - totalOborotDb;
        }
        private static void GetRate(IUbsDbConnection connection, IUbsWss ubs, DateTime dateRate, short idCurrencyDb, short idCurrencyCr, out decimal rateDb, out decimal nuDb, out decimal rateCr, out decimal nuCr) {
            rateDb = nuDb = rateCr = nuCr = 1;
            if (idCurrencyDb == 810 && idCurrencyCr == 810) return;

            connection.ClearParameters();
            connection.CmdText =
                "select ID_CURRENCY, RATE_NU, NU from COM_RATES_CB" +
                " where ID_CURRENCY in (@ID_CURRENCY_DB, @ID_CURRENCY_CR)" +
                    " and DATE_RATE <= @DATE_RATE and DATE_NEXT > @DATE_RATE";
            connection.AddInputParameter("DATE_RATE", System.Data.SqlDbType.DateTime, dateRate);
            connection.AddInputParameter("ID_CURRENCY_DB", System.Data.SqlDbType.SmallInt, idCurrencyDb);
            connection.AddInputParameter("ID_CURRENCY_CR", System.Data.SqlDbType.SmallInt, idCurrencyCr);
            connection.ExecuteUbsDbReader();
            while (connection.Read()) {
                if (idCurrencyDb == connection.GetInt16(0)) {
                    rateDb = connection.GetDecimal(1);
                    nuDb = connection.GetDecimal(2);
                }
                else {
                    rateCr = connection.GetDecimal(1);
                    nuCr = connection.GetDecimal(2);
                }
            }
            connection.CloseReader();
            connection.CmdReset();
        }

        /// <summary>
        /// Прогнозируемые обороты по счету
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="razdel">Номер раздела 0 - А, 1 - Б, 2 - B, 3 - Г, 4 - Д</param>
        /// <param name="idAccount">Идентификатор счета</param>
        /// <param name="dateEnd">Дата конца интервала</param>
        /// <param name="excludesIdDocuments">Массив идентификаторов документов исключаемых из поиска</param>
        /// <param name="totalOborotDb">Плановый дебетовый оборот</param>
        /// <param name="totalOborotCr">Плановый кредитовый оборот</param>
        /// <param name="totalOborot">Плановый оборот = (Плановый кредитовый оборот - Плановый дебетовый оборот)</param>
        /// <param name="blockedAmountAccount">Заблокированная сумма по счету</param>
        [Obsolete("Метод устарел, следует использовать метод GetAccountPrognosis с параметрами учета прогнозируемого остатка AccountPrognosis", true)]
        public static void GetPrognosisOborot(IUbsDbConnection connection, IUbsWss ubs, byte razdel, int idAccount, DateTime dateEnd, int[] excludesIdDocuments, out decimal totalOborotDb, out decimal totalOborotCr, out decimal totalOborot, out decimal blockedAmountAccount) {
            bool includePlatDoc = false, includeDocCard = false, includeBlockedAmount = false;

            if (razdel == 0) {
                object[,] item2 = (object[,])ubs.UbsWssParam("Установка", "Операционный день", "Режим определения прогнозируемого остатка");
                for (int i = 0; i <= item2.GetUpperBound(1); i++) {
                    string key = Convert.ToString(item2[0, i]).Trim();
                    bool isUse = Convert.ToInt32(item2[1, i]) > 0;

                    if ("Платежные документы".Equals(key, StringComparison.OrdinalIgnoreCase)) includePlatDoc = isUse;
                    else if ("Блокированные суммы".Equals(key, StringComparison.OrdinalIgnoreCase)) includeBlockedAmount = isUse;
                    else if ("Картотечные документы".Equals(key, StringComparison.OrdinalIgnoreCase)) includeDocCard = isUse;
                }
            }
            
            decimal blockedAmountTax, blockedAmountBS, sum1, sum2;
            bool isArrestAccount;
            GetPrognosisOborot(connection, ubs, razdel, idAccount, dateEnd, excludesIdDocuments, null, includePlatDoc, includeDocCard, includeBlockedAmount, out totalOborotDb, out totalOborotCr, out isArrestAccount, out blockedAmountTax, out blockedAmountBS, out sum1, out sum2);

            totalOborot = totalOborotCr - totalOborotDb;
            blockedAmountAccount = blockedAmountBS + blockedAmountTax;
        }
        /// <summary>
        /// Прогнозируемые обороты по счету
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="razdel">Номер раздела 0 - А, 1 - Б, 2 - B, 3 - Г, 4 - Д</param>
        /// <param name="idAccount">Идентификатор счета</param>
        /// <param name="dateEnd">Дата конца интервала</param>
        /// <param name="excludesIdDocuments">Массив идентификаторов документов исключаемых из поиска</param>
        /// <param name="includesKindDocs">Массив видов документов для поиска</param>
        /// <param name="includePlatDoc">Учесть платежные документы</param>
        /// <param name="includeDocCard">Учесть документы картотеки</param>
        /// <param name="includeBlockedAmount">Учесть заблокированные суммы</param>
        /// <param name="totalOborotDb">Плановый дебетовый оборот</param>
        /// <param name="totalOborotCr">Плановый кредитовый оборот</param>
        /// <param name="isArrestAccount">Признак ареста счета ИФНС</param>
        /// <param name="blockedAmmontTax">Сумма приостановлений ИФНС в валюте счета</param>
        /// <param name="blockedAmmontBS">Сумма приостановлений ССП в валюте счета</param>
        /// <param name="sum1">Сумма картотеки 1</param>
        /// <param name="sum2">Сумма картотеки 2</param>
        [Obsolete("Метод устарел, следует использовать метод GetAccountPrognosis с параметрами учета прогнозируемого остатка AccountPrognosis", true)]
        public static void GetPrognosisOborot(IUbsDbConnection connection, IUbsWss ubs, byte razdel, int idAccount, DateTime dateEnd, int[] excludesIdDocuments, byte[] includesKindDocs, bool includePlatDoc, bool includeDocCard, bool includeBlockedAmount, out decimal totalOborotDb, out decimal totalOborotCr, out bool isArrestAccount, out decimal blockedAmmontTax, out decimal blockedAmmontBS, out decimal sum1, out decimal sum2) {
            isArrestAccount = false;
            blockedAmmontTax = blockedAmmontBS = 0;
            totalOborotDb = totalOborotCr = 0;
            sum1 = sum2 = 0;

            if (razdel == 0) {
                if (includePlatDoc) // Обороты по счету
                    GetPlanOborotA(connection, ubs, idAccount, dateEnd, excludesIdDocuments, includesKindDocs, includeDocCard, out totalOborotDb, out totalOborotCr);

                if (includeBlockedAmount) // Заблокированная сумма по счету
                {
                    decimal blockedAmmontFTS;
                    GetBlockedAmountAccount(connection, ubs, idAccount, dateEnd, out isArrestAccount, out blockedAmmontTax, out blockedAmmontBS, out blockedAmmontFTS);
                    blockedAmmontTax += blockedAmmontFTS;
                }

                GetSumCardIndex(connection, ubs, idAccount, out sum1, out sum2);
            }
            else {
                GetPlanOborot(connection, ubs, razdel, idAccount, dateEnd, excludesIdDocuments, out totalOborotDb, out totalOborotCr);
            }
        }

        /// <summary>
        /// Параметры учета прогнозируемого остатка 
        /// </summary>
        public class AccountPrognosis {
            /// <summary>
            /// Конструктор
            /// </summary>
            public AccountPrognosis() {
                this.WhereEndDate = dt22220101;
                this.WhereKindDocumentAre = new List<byte>();
                this.WhereIdDocumentNotAre = new List<int>();
            }

            /// <summary>
            /// Выставить все признаки учета сумм
            /// </summary>
            /// <param name="needed">Требуемое значение учета</param>
            public void SetAllNeedSum(bool needed) {
                this.NeedOborotSum =
                    this.NeedCardIndexSum =
                        this.NeedOborotCardIndexSum =
                            this.NeedBlockedSum =
                                this.NeedComissionSumRKO =
                                    this.NeedSaldoSum = this.NeedMinimumSaldoSum = needed;
            }

            /// <summary>
            /// Дата конца интервала поиска
            /// </summary>
            public DateTime? WhereEndDate { get; set; }
            /// <summary>
            ///  Виды документов включаемые в поиск
            /// </summary>
            public List<byte> WhereKindDocumentAre { get; private set; }
            /// <summary>
            ///  Документы исключаемые из поиска
            /// </summary>
            public List<int> WhereIdDocumentNotAre { get; private set; }
            /// <summary>
            /// Учесть обороты по документам
            /// </summary>
            public bool? NeedOborotSum { get; set; }
            /// <summary>
            /// Учесть задолжность по картотеки
            /// </summary>
            public bool? NeedCardIndexSum { get; set; }
            /// <summary>
            /// Учесть обороты по документам на картотеке
            /// </summary>
            public bool? NeedOborotCardIndexSum { get; set; }
            /// <summary>
            /// Учесть приостановления
            /// </summary>
            public bool? NeedBlockedSum { get; set; }
            /// <summary>
            /// Учесть рассчитанные комиссии за РКО
            /// </summary>
            public bool? NeedComissionSumRKO { get; set; }
            /// <summary>
            /// Учесть остаток по счету
            /// </summary>
            public bool? NeedSaldoSum { get; set; }
            /// <summary>
            /// Учесть минимальный остаток по счету
            /// </summary>
            public bool? NeedMinimumSaldoSum { get; set; }
            /// <summary>
            /// Учесть информацию по счету
            /// </summary>
            public bool? NeedAccountInformation { get; set; }

            /// <summary>
            /// Сумма оборотов по ДБ
            /// </summary>
            public decimal SumOborotDb { get; internal set; }
            /// <summary>
            /// Сумма оборотов по КР
            /// </summary>
            public decimal SumOborotCr { get; internal set; }
            /// <summary>
            /// Сумма картотеки 1 
            /// </summary>
            public decimal SumCardIndex1 { get; internal set; }
            /// <summary>
            /// Сумма картотеки 2
            /// </summary>
            public decimal SumCardIndex2 { get; internal set; }
            /// <summary>
            /// Сумма картотеки 1 ДБ
            /// </summary>
            public decimal SumOborotCardIndex1Db { get; internal set; }
            /// <summary>
            /// Сумма картотеки 1 КР
            /// </summary>
            public decimal SumOborotCardIndex1Cr { get; internal set; }
            /// <summary>
            /// Сумма картотеки 2 ДБ
            /// </summary>
            public decimal SumOborotCardIndex2Db { get; internal set; }
            /// <summary>
            /// Сумма картотеки 2 КР
            /// </summary>
            public decimal SumOborotCardIndex2Cr { get; internal set; }
            /// <summary>
            /// Сумма валютной картотеки 2
            /// </summary>
            public decimal SumCardIndexCurrency2 { get; internal set; }
            /// <summary>
            /// Сумма валютной картотеки 2 в валюте счета
            /// </summary>
            public decimal SumCardIndexCurrency2InCurrency { get; internal set; }
            /// <summary>
            /// Счет арестован ИФНС
            /// </summary>
            public bool IsArrest { get; internal set; }
            /// <summary>
            /// Сумма приостановлений ИФНС (в валюте счета)
            /// </summary>
            public decimal SumBlockedTax { get; internal set; }
            /// <summary>
            /// Сумма приостановлений ССП (в валюте счета)
            /// </summary>
            public decimal SumBlockedBailiffsService { get; internal set; }
            /// <summary>
            /// Сумма приостановлений ФТС (в валюте счета)
            /// </summary>
            public decimal SumBlockedFTS { get; internal set; }
            /// <summary>
            /// Сумма комиссий за РКО
            /// </summary>
            public decimal SumComissionRKO { get; internal set; }
            /// <summary>
            /// Идентификатор договора РКО
            /// </summary>
            public int ContractIdRKO { get; internal set; }
            /// <summary>
            /// Сумма остатка по счету
            /// </summary>
            public decimal SumSaldo { get; internal set; }
            /// <summary>
            /// Сумма минимального остатка по счету
            /// </summary>
            public decimal SumMinimumSaldo { get; internal set; }
            /// <summary>
            /// Сумма максимального остатка по счету
            /// </summary>
            public decimal SumMaximumSaldo { get; internal set; }
            /// <summary>
            /// Сумма овердрафта остатка по счету
            /// </summary>
            public decimal SumOverdraftSaldo  { get; internal set; }
            /// <summary>
            /// Активность счета
            /// </summary>
            public byte Activity { get; internal set; }
            /// <summary>
            /// Состояние счета (текстовое представление)
            /// открыт, закрыт, заблокирован, заморожен, зарезервирован, удален
            /// </summary>
            public string StateText { get; internal set; }
            /// <summary>
            /// Вид проверки остатка счета
            /// красное сальдо, минимальный остаток, овердрафт, нет проверки, максимальный остаток
            /// </summary>
            public string KindСheckingSaldo { get; internal set; }
            /// <summary>
            /// Номер счета
            /// </summary>
            public string AccountNumber { get; internal set; }
         }

        /// <summary>
        /// Прогнозируемые обороты по счету
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="razdel">Номер раздела 0 - А, 1 - Б, 2 - B, 3 - Г, 4 - Д</param>
        /// <param name="accountId">Идентификатор счета</param>
        /// <param name="prognosis">Параметры учета прогнозируемого остатка</param>
        /// <returns>Сумма прогнозируемого остатка</returns>
        public static void GetAccountPrognosis(IUbsDbConnection connection, IUbsWss ubs, byte razdel, int accountId, AccountPrognosis prognosis) {
            if (prognosis.WhereEndDate == null || prognosis.WhereEndDate <= dt19900101 || prognosis.WhereEndDate >= dt22220101)
                prognosis.WhereEndDate = Convert.ToDateTime(ubs.UbsWssParam("CommonDate", "Server"));

            if (accountId == 0) throw new ArgumentNullException("accountId", "Идентификатор счета не задан");

            if (razdel == 0) {
                object[,] settings = (object[,])ubs.UbsWssParam("Установка", "Операционный день", "Режим определения прогнозируемого остатка");
                for (int i = 0; i <= settings.GetUpperBound(1); i++) {
                    string nameType = Convert.ToString(settings[0, i]).Trim();
                    bool needFlag = Convert.ToInt32(settings[1, i]) > 0;

                    if (prognosis.NeedOborotSum == null && "Платежные документы".Equals(nameType, StringComparison.OrdinalIgnoreCase)) prognosis.NeedOborotSum = needFlag;
                    else if (prognosis.NeedBlockedSum == null && "Блокированные суммы".Equals(nameType, StringComparison.OrdinalIgnoreCase)) prognosis.NeedBlockedSum = needFlag;
                    else if (prognosis.NeedCardIndexSum == null && "Картотечные документы".Equals(nameType, StringComparison.OrdinalIgnoreCase)) prognosis.NeedCardIndexSum = needFlag;
                    else if (prognosis.NeedComissionSumRKO == null && "Суммы рассчитанных комиссий".Equals(nameType, StringComparison.OrdinalIgnoreCase)) prognosis.NeedComissionSumRKO = needFlag;
                }

                // Обороты по документам
                if (prognosis.NeedOborotSum.HasValue && (bool)prognosis.NeedOborotSum) {

                    decimal sumOborotDb, sumOborotCr;

                    // Платежные документы
                    GetPlanOborotA(connection, ubs, accountId, (DateTime)prognosis.WhereEndDate
                        , prognosis.WhereIdDocumentNotAre.ToArray()
                        , prognosis.WhereKindDocumentAre.ToArray(), out sumOborotDb, out sumOborotCr);

                    prognosis.SumOborotDb = sumOborotDb;
                    prognosis.SumOborotCr = sumOborotCr;

                    // Валютные требования
                    GetPlanOborotCurrency(connection, ubs, accountId, (DateTime)prognosis.WhereEndDate
                        , prognosis.WhereIdDocumentNotAre.ToArray()
                        , out sumOborotDb, out sumOborotCr);

                    prognosis.SumOborotDb += sumOborotDb;
                    prognosis.SumOborotCr += sumOborotCr;
                }

                // Приостановления
                if (prognosis.NeedBlockedSum.HasValue && (bool)prognosis.NeedBlockedSum) {
                    bool isArrest;
                    decimal sumBlockedTax, sumBlockedBailiffsService, sumBlockedFTS;
                    GetBlockedAmountAccount(connection, ubs, accountId, (DateTime)prognosis.WhereEndDate, out isArrest, out sumBlockedTax, out sumBlockedBailiffsService, out sumBlockedFTS);

                    prognosis.SumBlockedTax = sumBlockedTax;
                    prognosis.SumBlockedBailiffsService = sumBlockedBailiffsService;
                    prognosis.SumBlockedFTS = sumBlockedFTS;
                    prognosis.IsArrest = isArrest;
                }

                // Картотека
                if (prognosis.NeedCardIndexSum.HasValue && (bool)prognosis.NeedCardIndexSum) {

                    decimal sumCardIndex1, sumCardIndex2;
                    GetSumCardIndex(connection, ubs, accountId, out sumCardIndex1, out sumCardIndex2);

                    prognosis.SumCardIndex1 = sumCardIndex1;
                    prognosis.SumCardIndex2 = sumCardIndex2;

                    decimal sumCardIndexCurrency2, sumCardIndexCurrency2inCurrency;

                    GetSumCardIndexCurrency(connection, ubs, accountId, (DateTime)prognosis.WhereEndDate
                        , prognosis.WhereIdDocumentNotAre.ToArray(), out sumCardIndexCurrency2
                        , out sumCardIndexCurrency2inCurrency);
                    prognosis.SumCardIndexCurrency2 = sumCardIndexCurrency2;
                    prognosis.SumCardIndexCurrency2InCurrency = sumCardIndexCurrency2inCurrency;
                }

                // Обороты картотеки
                if (prognosis.NeedOborotCardIndexSum.HasValue && (bool)prognosis.NeedOborotCardIndexSum) {
                    decimal sumCardIndex1Db, sumCardIndex1Cr, sumCardIndex2Db, sumCardIndex2Cr;
                    GetSumCardIndex(connection, ubs, accountId, prognosis.WhereIdDocumentNotAre.ToArray()
                        , out sumCardIndex1Db, out sumCardIndex1Cr, out sumCardIndex2Db, out sumCardIndex2Cr);

                    prognosis.SumOborotCardIndex1Db = sumCardIndex1Db;
                    prognosis.SumOborotCardIndex1Cr = sumCardIndex1Cr;
                    prognosis.SumOborotCardIndex2Db = sumCardIndex2Db;
                    prognosis.SumOborotCardIndex2Cr = sumCardIndex2Cr;
                }


                // Комиссии РКО
                if (prognosis.NeedComissionSumRKO.HasValue && (bool)prognosis.NeedComissionSumRKO) {
                    int contractIdRKO;
                    decimal sumComissionRKO;

                    GetSumCommissionRKO(connection, ubs, accountId, (DateTime)prognosis.WhereEndDate
                        , prognosis.WhereIdDocumentNotAre.ToArray(), out contractIdRKO, out sumComissionRKO);

                    prognosis.ContractIdRKO = contractIdRKO;
                    prognosis.SumComissionRKO = sumComissionRKO;
                }
            }
            else {
                decimal sumOborotDb, sumOborotCr;
                GetPlanOborot(connection, ubs, razdel, accountId, (DateTime)prognosis.WhereEndDate, prognosis.WhereIdDocumentNotAre.ToArray()
                    , out sumOborotDb, out sumOborotCr);

                prognosis.SumOborotDb = sumOborotDb;
                prognosis.SumOborotCr = sumOborotCr;
            }

            // Остаток по счету
            if (prognosis.NeedSaldoSum.HasValue && (bool)prognosis.NeedSaldoSum) {
                prognosis.SumSaldo = UbsOD_GetSaldo.GetSaldoTrn(connection, razdel, accountId, (DateTime)prognosis.WhereEndDate);
            }

            // Информация по счету
            if (prognosis.NeedAccountInformation.HasValue && (bool)prognosis.NeedAccountInformation ||
                prognosis.NeedMinimumSaldoSum.HasValue && (bool)prognosis.NeedMinimumSaldoSum) {
                UbsODAccount account = new UbsODAccount(connection, ubs, razdel);
                account.Read(accountId);

                prognosis.AccountNumber = account.StrAccount;
                prognosis.Activity = account.Activ;
                prognosis.StateText = account.State;
                prognosis.KindСheckingSaldo = account.VerSaldo;

                // В случае если срок действия лимита задан и истек проверка на красное сальдо
                if (prognosis.WhereEndDate.Value > account.DateLimit) prognosis.KindСheckingSaldo = "красное сальдо";

                if (razdel == 0) {
                    // account.Limit;
                    // account.DateLimit;
                    if (account.DateLimit >= prognosis.WhereEndDate) {
                        if ("минимальный остаток".Equals(account.VerSaldo, StringComparison.OrdinalIgnoreCase))
                            prognosis.SumMinimumSaldo = Math.Abs(account.Limit);
                        else if ("овердрафт".Equals(account.VerSaldo, StringComparison.OrdinalIgnoreCase))
                            prognosis.SumOverdraftSaldo = Math.Abs(account.Limit);
                        else if ("максимальный остаток".Equals(account.VerSaldo, StringComparison.OrdinalIgnoreCase))
                            prognosis.SumMaximumSaldo = Math.Abs(account.Limit);
                    }
                }
            }
            /*
            return prognosis.SumSaldo
                 + prognosis.SumOborotCr
                 - prognosis.SumOborotDb
                 - prognosis.SumBlockedBailiffsService
                 - prognosis.SumBlockedTax + prognosis.SumBlockedFTS
                 - prognosis.SumComissionRKO
                 - prognosis.SumCardIndexCurrency2InCurrency
                 - prognosis.SumCardIndex1
                 - prognosis.SumCardIndex2;*/
        }

        /// <summary>
        /// Проверить подходит ли документ раздела А под условия разрешающие списания при блокировке ИФНС
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с СП</param>
        /// <param name="document">Платежный документ раздела А</param>
        /// <param name="needControl">Признак необходимости постановки документа на ручной контроль</param>
        /// <param name="comment">Причина для постановкуи документа на ручной контроль</param>
        /// <returns>true - документ подходит под условия</returns>
        public static bool FindConditionIFNS(IUbsDbConnection connection, IUbsWss ubs, UbsODPayDoc document, out bool needControl, out string comment) {
            string recipientAccount = document.KindDoc == 8 ? (string)document.Field("N сч. получателя") : document.Account_R;
            if (string.IsNullOrEmpty(recipientAccount)) recipientAccount = document.Account_CR;
            recipientAccount.Trim();
            string bal2 = string.IsNullOrEmpty(recipientAccount) ? string.Empty : recipientAccount.Substring(0, 5);

            return FindConditionIFNS(connection, ubs, document.KindDoc, document.PriorityPay, bal2, (string)document.Field("Код бюджетной классификации"), out needControl, out comment);
        }


        private static bool FindConditionIFNS(IUbsDbConnection connection, IUbsWss ubs, byte kinddoc, byte ocherPaym, string bal2, string kbk, out bool needControl, out string comment) {
            object[,] settingsDebtBlock = (object[,])ubs.UbsWssParam("Установка", "Операционный день", "Списание при блок. ИФНС");
        
            needControl = false;
            comment = null;
            if (settingsDebtBlock == null) return false;

            int total = 0, totalControl = 0;
            StringBuilder comments = new StringBuilder();

            for(int i = 0; i <= settingsDebtBlock.GetUpperBound(1); i++) {

                // По виду документа
                string value = ((string)settingsDebtBlock[0, i] ?? "").Trim();
                bool flagKindDoc = string.IsNullOrEmpty(value) ? true : Regex.IsMatch(value, string.Format("\\b{0}\\b", kinddoc));
                    
                // По очередности
                value = ((string)settingsDebtBlock[1, i] ?? "").Trim();
                bool flagOcherPaym = string.IsNullOrEmpty(value) ? true : Regex.IsMatch(value, string.Format("\\b{0}\\b", ocherPaym));

                // По балансу
                value = ((string)settingsDebtBlock[2, i] ?? "").Trim();
                bool flagBal2 = string.IsNullOrEmpty(value) ? true : (string.IsNullOrEmpty(bal2) ? false : Regex.IsMatch(value, string.Format("\\b{0}\\b", bal2)));

                // Проверяем КБК (для скорости проверяем если совпадение было по остальным признакам, иначе нет смысла)
                value = ((string)settingsDebtBlock[3, i] ?? "").Trim();
                bool flagKbk = false;
                bool signKbk = "ДА".Equals(value, StringComparison.OrdinalIgnoreCase);
                if (flagKindDoc && flagOcherPaym && flagBal2 && signKbk) {
                    connection.ClearParameters();
                    connection.CmdText = "select k.ISDIRECT from OD_KBK k where k.KBK = @KBK";
                    connection.AddInputParameter("KBK", System.Data.SqlDbType.VarChar, kbk ?? "");
                    object scalar = connection.ExecuteScalar();
                    if (scalar != null && Convert.ToBoolean(scalar)) flagKbk = true;
                }

                // Совпадение полное
                if (flagKindDoc && flagOcherPaym && flagBal2 && (!signKbk || signKbk && flagKbk)) {
                    total++;

                    value = ((string)settingsDebtBlock[4, i] ?? "").Trim();
                    if("ДА".Equals(value, StringComparison.OrdinalIgnoreCase)) totalControl ++;
                    value = ((string)settingsDebtBlock[5, i] ?? "").Trim();

                    if (!string.IsNullOrEmpty(value)){
                        if (comments.Length > 0) comments.Append(",");
                        comments.Append(value);
                    }
                }
            }

            if (total > 0) {
                needControl = totalControl == total;
                comment = comments.ToString();
                return true;
            }
            return false;
        }
        /*
        /// <summary>
        /// Параметры платежа
        /// </summary>
        public class PaymentParameters {
            /// <summary>
            /// Счет дебет
            /// </summary>
            public string AccountDebit { get; set; }
            /// <summary>
            /// Счет кредит если не задан счет получателя
            /// </summary>
            public string AccountCredit { get; set; }
            /// <summary>
            /// Счет получателя
            /// </summary>
            public string AccountRecipient { get; set; }
            /// <summary>
            /// Шифр документа
            /// </summary>
            public byte Kinddoc { get; set; }
            /// <summary>
            /// КБК
            /// </summary>
            public string Kbk { get; set; }
            /// <summary>
            /// Очередность платежа
            /// </summary>
            public byte OcherPaym { get; set; }
            /// <summary>
            /// Обород по дебету
            /// </summary>
            public decimal OborotDebit { get; set; }
        }*/


        
       
        /// <summary>
        /// Проверка наличия приостановлений ИФНС и ареста суммы ССП по счетам клиента
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="idClient">Идентификатор клиента</param>
        /// <param name="dateCheck">Дата проверки</param>
        /// <returns>маска наличия 0 - нет, 1 - бит арест ИФНС на сумму, 2 - бит арест ССП на сумму, 3 - бит ИФНС - арест счета, 4 - бит арест ФТС на сумму, 5 - бит ФТС - арест счета </returns>
        public static int CheckBlockedAccount(IUbsDbConnection connection, IUbsWss ubs, int idClient, DateTime dateCheck) {
            connection.ClearParameters();
            connection.CmdText =
                "select ID_ACCOUNT from OD_ACCOUNTS0 a where a.IS_BLOK_SUM = 1 and a.ID_CLIENT = @ID_CLIENT";
            connection.AddInputParameter("ID_CLIENT", System.Data.SqlDbType.Int, idClient);
            List<object> listIdsAccounts = connection.ExecuteReadListRec();
            connection.CmdReset();

            if (listIdsAccounts.Count == 0) return 0; // Приостановлений нет

            int result = 0;

            connection.ClearParameters();
            connection.CmdText =
                "select b.TYPE_SOURCE, b.SUMMA from OD_ACCOUNTS0_BLOK_SUM b" +
                    " where b.DATE_SET <= @DATE_CHECK and b.DATE_END > @DATE_CHECK and b.ID_ACCOUNT in (@ID_ACCOUNT)";
            connection.AddInputParameter("DATE_CHECK", System.Data.SqlDbType.DateTime, dateCheck);
            connection.AddInputParameterIN("ID_ACCOUNT", System.Data.SqlDbType.Int, listIdsAccounts.ToArray());
            connection.ExecuteUbsDbReader();
            while (connection.Read()) {
                decimal sum = connection.GetDecimal(1);
                switch (connection.GetByte(0)) {
                    case 1: result = result | 0x02; break;
                    case 2: result = result | (sum > 0 ? 0x01 : 0x04) ; break;
                    case 3: result = result | (sum > 0 ? 0x08 : 0x10); break;
                }
            }
            connection.CloseReader();
            connection.CmdReset();

            return result;
        }

        /// <summary>
        /// Проверка суммы дополнительного контроля
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="bicR">БИК банка получателя</param>
        /// <param name="sum">Сумма документа в рублях</param>
        /// <param name="kindClient">Вид клиента</param>
        /// <returns>Результат проверки true - проверка пройдена, false - проверка не пройдена требуется дополнительный контроль</returns>
        public static bool CheckSumControl(IUbsDbConnection connection, IUbsWss ubs, string bicR, decimal sum, byte kindClient) {

            if (string.IsNullOrEmpty(bicR)) throw new UbsObjectException("Бик банка получателя не указан");

            string settingBicBank = Convert.ToString(ubs.UbsWssParam("Установка", "Основные установки", "БИК банка"));

            if (bicR.Equals(settingBicBank, StringComparison.OrdinalIgnoreCase)) return true;

            object[] settingExclBicBank = (object[])ubs.UbsWssParam("Установка", "Операционный день", "Пороговая сумма контроля - исключаемый БИК");
            if (settingExclBicBank != null) {
                foreach (object item in settingExclBicBank) {
                    if (bicR.Equals((string)item, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            object[,] record = (object[,])ubs.UbsWssParam("Установка", "Операционный день", "Пороговая сумма контроля");

            if (kindClient == 1 && sum > Convert.ToDecimal(record[0, 0]) && Convert.ToDecimal(record[0, 0]) > 0 ||
               kindClient == 2 && sum > Convert.ToDecimal(record[1, 0]) && Convert.ToDecimal(record[1, 0]) > 0) {
                return false;
            }
               
            return true;
        }

        /// <summary>
        /// Проверка контроля документа
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <param name="sidControl">СИД вида запрашиваемого контроля</param>
        /// <returns>Контроль не устанавливался (-1), контроль пройден (0), стадия прохождения контроля (>0)</returns>
        public static int GetStateControl(IUbsDbConnection connection, IUbsWss ubs, int documentId, string sidControl) {
            connection.ClearParameters();
            connection.CmdText = "select d.KIND_CONTROL from OD_DOC_0_CTRL_DIC d where d.SID_CONTROL = @SID_CONTROL";
            connection.AddInputParameter("SID_CONTROL", System.Data.SqlDbType.VarChar, sidControl);
            object scalar = connection.ExecuteScalar();
            if (scalar == null)
                throw new UbsObjectException(string.Format("Вид контроля со строковым идентификатором <{0}> не найден", sidControl));
            
            short kindControl = Convert.ToInt16(scalar);

            connection.ClearParameters();
            connection.CmdText = "select c.STATE_CONTROL from OD_DOC_0_CTRL c where c.KIND_CONTROL = @KIND_CONTROL and c.ID_DOC = @ID_DOC";
            connection.AddInputParameter("KIND_CONTROL", System.Data.SqlDbType.SmallInt, kindControl);
            connection.AddInputParameter("ID_DOC", System.Data.SqlDbType.Int, documentId);
            scalar = connection.ExecuteScalar();
            if (scalar == null) return -1;
            
            return Convert.ToByte(scalar);
        }

           /// <summary>
        /// Установка стадии контроля документа
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <param name="sidControl">СИД вида запрашиваемого контроля</param>
        /// <param name="state">Стадия прохождения контроля</param>
        public static void SetStateControl(IUbsDbConnection connection, IUbsWss ubs, int documentId, string sidControl, int state) {
            UbsODPayDoc document = new UbsODPayDoc(connection, ubs, 0);
            document.Read(documentId);
            SetStateControl(connection, ubs, document, sidControl, state, null, null);
        }

        /// <summary>
        /// Установка стадии контроля документа
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="document">Документа раздела А</param>
        /// <param name="sidControl">СИД вида запрашиваемого контроля</param>
        /// <param name="state">Стадия прохождения контроля</param>
        /// <param name="reason">Причина постановки на контроль</param>
        /// <param name="rejectReason">Причина отказа прохождения контроля</param>
        public static void SetStateControl(IUbsDbConnection connection, IUbsWss ubs, UbsODPayDoc document, string sidControl, int state, string reason, string rejectReason) {
            connection.ClearParameters();
            connection.CmdText = "select d.KIND_CONTROL from OD_DOC_0_CTRL_DIC d where d.SID_CONTROL = '" + sidControl + "'";
            object scalar = connection.ExecuteScalar();
            if (scalar == null)
                throw new UbsObjectException(string.Format("Вид контроля со строковым идентификатором <{0}> не найден", sidControl));

            short kindControl = Convert.ToInt16(scalar);

            using(UbsTransact transaction = new UbsTransact(connection)) {
                connection.CmdText = "delete OD_DOC_0_CTRL where KIND_CONTROL = " + kindControl + " and ID_DOC = " + document.Id;
                connection.ExecuteScalar();

                connection.CmdText = 
                    "insert into OD_DOC_0_CTRL (ID_DOC, KIND_CONTROL, STATE_CONTROL, ID_USER_CONTROL, TIME_CONTROL)" +
                    " values(" + document.Id + ", " + kindControl + ", " + state + ", " + ubs.UbsWssParam("IdUser") + ", getdate())" ;
                connection.ExecuteScalar();

                decimal oborotDb = document.SummaDB, oborotCr = document.SummaCR;

                connection.CmdText = 
                    "insert into OD_DOC_0_CTRL_HISTORY(ID_DOC, KIND_CONTROL, KINDDOC, TYPE_DOC, OBOROT_DB, OBOROT_CR, DATE_DOC, NUM_DOC" +
                        ", STRACCOUNT_P, NAME_P, INN_P, STRACCOUNT_R, NAME_R, INN_R, CTRL_REASON, CTRL_STATE, CTRL_REJECT_REASON" +
                        ", ID_USER_CTRL, TIME_CTRL)" +
                    " values(@ID_DOC, @KIND_CONTROL, @KINDDOC, @TYPE_DOC, @OBOROT_DB, @OBOROT_CR, @DATE_DOC, @NUM_DOC" +
                        ", @STRACCOUNT_P, @NAME_P, @INN_P, @STRACCOUNT_R, @NAME_R, @INN_R, @CTRL_REASON, @CTRL_STATE, @CTRL_REJECT_REASON" +
                        ", " + ubs.UbsWssParam("IdUser") + ", getdate())" ;
                connection.AddInputParameter("ID_DOC", System.Data.SqlDbType.Int, document.Id);
                connection.AddInputParameter("KIND_CONTROL", System.Data.SqlDbType.SmallInt, kindControl);
                connection.AddInputParameter("KINDDOC", System.Data.SqlDbType.TinyInt, document.KindDoc);
                connection.AddInputParameter("TYPE_DOC", System.Data.SqlDbType.TinyInt, document.TypeDoc);
                connection.AddInputParameter("OBOROT_DB", System.Data.SqlDbType.Decimal, oborotDb);
                connection.AddInputParameter("OBOROT_CR", System.Data.SqlDbType.Decimal, oborotCr);
                connection.AddInputParameter("DATE_DOC", System.Data.SqlDbType.DateTime, document.DateDoc);
                connection.AddInputParameter("NUM_DOC", System.Data.SqlDbType.VarChar, document.Number);
                connection.AddInputParameter("STRACCOUNT_P", System.Data.SqlDbType.VarChar, string.IsNullOrEmpty(document.Account_P) ? document.Account_DB : document.Account_P);
                connection.AddInputParameter("NAME_P", System.Data.SqlDbType.VarChar, document.Name_P);
                connection.AddInputParameter("INN_P", System.Data.SqlDbType.VarChar, document.INN_P);
                connection.AddInputParameter("STRACCOUNT_R", System.Data.SqlDbType.VarChar, string.IsNullOrEmpty(document.Account_R) ? document.Account_CR : document.Account_R);
                connection.AddInputParameter("NAME_R", System.Data.SqlDbType.VarChar, document.Name_R);
                connection.AddInputParameter("INN_R", System.Data.SqlDbType.VarChar, document.INN_R);
                if (string.IsNullOrEmpty(reason)) connection.AddInputParameter("CTRL_REASON", DBNull.Value); else connection.AddInputParameter("CTRL_REASON", System.Data.SqlDbType.VarChar, reason);
                connection.AddInputParameter("CTRL_STATE", System.Data.SqlDbType.TinyInt, state);
                if (string.IsNullOrEmpty(rejectReason)) connection.AddInputParameter("CTRL_REJECT_REASON", DBNull.Value); else connection.AddInputParameter("CTRL_REJECT_REASON", System.Data.SqlDbType.VarChar, rejectReason);
                connection.ExecuteScalar();

                transaction.Commit();
            }
        }

        /// <summary>
        /// Проверка прохождения террористического контроля
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <returns>true - признак причастности документа к террористической деятельности</returns>
        [Obsolete("Следует использовать метод static bool CheckMarkTerroristActivities(IUbsDbConnection connection, IUbsWss ubs, int documentId)", true)]
        public static bool IsTerror(IUbsDbConnection connection, IUbsWss ubs, int documentId) {
            return !CheckMarkTerroristActivities(connection, ubs, documentId);
            //connection.ClearParameters();
            //connection.CmdText = "select count(*) from OD_DOC_0_CHECK_TERROR where IS_TERRORIST = 1 and ID_DOC = @ID_DOC";
            //connection.AddInputParameter("ID_DOC", System.Data.SqlDbType.Int, documentId);
            //return Convert.ToInt32(connection.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// Получить список статусов состовителей (список значений поля 101)
        /// </summary>
        /// <returns>Массив массивов</returns>
        public static object[] GetListStatusCreater() {
            List<object> items = new List<object>();
            items.Add(new object[] { "00", "00 - органами контроля за уплатой страховых взносов, оформившие инкассовое поручение на обязательное пенсионное и обязательное медицинское страхование, а также страховых взносов на обязательное социальное страхование на случай временное нетрудоспособности и в связи с материнством" });
            items.Add(new object[] { "01", "01 - налогоплательщик (плательщик сборов) - юридическое лицо" });
            items.Add(new object[] { "02", "02 - налоговый агент" });
            items.Add(new object[] { "03", "03 - организация федеральной почтовой связи, оформившая расчетный документ на перечисление в бюджетную систему Российской Федерации налогов, сборов, таможенных и иных платежей от внешнеэкономической деятельности (далее - таможенные платежи) и иных платежей, уплачиваемых физическими лицами" });
            items.Add(new object[] { "04", "04 - налоговый орган" });
            items.Add(new object[] { "05", "05 - территориальные органы Федеральной службы судебных приставов" });
            items.Add(new object[] { "06", "06 - участник внешнеэкономической деятельности - юридическое лицо" });
            items.Add(new object[] { "07", "07 - таможенный орган" });
            items.Add(new object[] { "08", "08 - плательщик иных платежей, осуществляющий перечисление платежей в бюджетную систему Российской Федерации (кроме платежей, администрируемых налоговыми органами)" });
            items.Add(new object[] { "09", "09 - налогоплательщик (плательщик сборов) - индивидуальный предприниматель" });
            items.Add(new object[] { "10", "10 - налогоплательщик (плательщик сборов) - нотариус, занимающийся частной практикой" });
            items.Add(new object[] { "11", "11 - налогоплательщик (плательщик сборов) - адвокат, учредивший адвокатский кабинет" });
            items.Add(new object[] { "12", "12 - налогоплательщик (плательщик сборов) - глава крестьянского (фермерского) хозяйства" });
            items.Add(new object[] { "13", "13 - налогоплательщик (плательщик сборов) - иное физическое лицо - клиент банка (владелец счета)" });
            items.Add(new object[] { "14", "14 - налогоплательщик, производящий выплаты физическим лицам (подпункт 1 пункта 1 статьи 235 Налогового кодекса Российской Федерации)" });
            items.Add(new object[] { "15", "15 - кредитная организация (ее филиал), оформившая расчетный документ на общую сумму на перечисление в бюджетную систему Российской Федерации налогов, сборов, таможенных платежей и иных платежей, уплачиваемых физическими лицами без открытия банковского счета" });
            items.Add(new object[] { "16", "16 - участник внешнеэкономической деятельности - физическое лицо" });
            items.Add(new object[] { "17", "17 - участник внешнеэкономической деятельности - индивидуальный предприниматель" });
            items.Add(new object[] { "18", "18 - плательщик таможенных платежей, не являющийся декларантом, на которого законодательством Российской Федерации возложена обязанность по уплате таможенных платежей" });
            items.Add(new object[] { "19", "19 - организации и их филиалы (далее - организации), оформившие расчетный документ на перечисление на счет органа Федерального казначейства денежных средств, удержанных из заработка (дохода) должника - физического лица в счет погашения задолженности по таможенным платежам на основании исполнительного документа, направленного в организацию в установленном порядке" });
            items.Add(new object[] { "20", "20 - кредитная организация (ее филиал), оформившая расчетный документ по каждому платежу физического лица на перечисление таможенных платежей, уплачиваемых физическими лицами без открытия банковского счета" });
            items.Add(new object[] { "21", "21 - ответственный участник консолидированной группы налогоплательщиков" });
            items.Add(new object[] { "22", "22 - участник консолидированной группы налогоплательщиков" });
            items.Add(new object[] { "23", "23 - органы контроля за уплатой страховых взносов" });
            items.Add(new object[] { "24", "24 - плательщик - физическое лицо, осуществляющее перевод денежных средств в уплату страховых взносов и иных платежей в бюджетную систему РФ" });
            items.Add(new object[] { "25", "25 - банки-гаранты, составившие распоряжение о переводе денежных средств в бюджетную систему РФ при возврате налога на добавленную стоимость, излишне полученной налогоплательщиком (зачтенной ему) в заявительном порядке, а также при уплате акцизов, исчисленных по операциям реализации подакцизных товаров за пределы территории РФ, и акцизов по алкогольной и (или) подакцизной спиртосодержащей продукции" });
            items.Add(new object[] { "26", "26 - учредители (участники) должника, собственники имущества должника - унитарного предприятия или третьи лица, составившие распоряжение о переводе денежных средств на погашение требований к должнику по уплате обязательных платежей, включенных в реестр требований кредиторов, в ходе процедур, применяемых в деле о банкротстве" });

            return items.ToArray();
        }

        /// <summary>
        /// Проверка назначения платежа на содержание в нем реквизитов доп. соглашения на безакцептное списание (номера договора, даты договора, пункта договора)
        /// </summary>
        /// <param name="dateTransaction">Дата проверки</param>
        /// <param name="payLocate">Направление документа</param>
        /// <param name="payerAccount">Счет списания</param>
        /// <param name="recipientAccount">Счет зачисления</param>
        /// <param name="bicExternalBank">БИК внешнего банка</param>
        /// <param name="description">Назначение платежа</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <param name="additionalAgreement">Доп. соглашение</param>
        /// <returns>true - проверка пройдена</returns>
        [Obsolete("Следует использовать метод bool CheckNonAcceptanceDebitAdditionalAgreement(DateTime dateTransaction, byte payLocate, string payerAccount, string recipientAccount, string bicExternalBank, string description, string inn, out string message, out UbsParam additionalAgreement)", true)]
        public bool CheckNonAcceptanceDebitAdditionalAgreement(DateTime dateTransaction, byte payLocate, string payerAccount, string recipientAccount, string bicExternalBank, string description, out string message, out UbsParam additionalAgreement) {
            return CheckNonAcceptanceDebitAdditionalAgreement(dateTransaction, payLocate, payerAccount, recipientAccount, bicExternalBank, description, null, out message, out additionalAgreement);
        }

        /// <summary>
        /// Проверка назначения платежа на содержание в нем реквизитов доп. соглашения на безакцептное списание (номера договора, даты договора, пункта договора)
        /// </summary>
        /// <param name="dateTransaction">Дата проверки</param>
        /// <param name="payLocate">Направление документа</param>
        /// <param name="payerAccount">Счет списания</param>
        /// <param name="recipientAccount">Счет зачисления</param>
        /// <param name="bicExternalBank">БИК внешнего банка</param>
        /// <param name="description">Назначение платежа</param>
        /// <param name="recipientInn">ИНН клиента счета зачисления</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <param name="additionalAgreement">Доп. соглашение</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckNonAcceptanceDebitAdditionalAgreement(DateTime dateTransaction, byte payLocate, string payerAccount, string recipientAccount, string bicExternalBank, string description, string recipientInn, out string message, out UbsParam additionalAgreement) {
            message = null;
            additionalAgreement = null;

            if (payLocate == 0 && string.IsNullOrEmpty(bicExternalBank)) bicExternalBank = this.settingBicBank;
            if (payLocate == 0 && string.IsNullOrEmpty(recipientInn)) recipientInn = this.settingInnBank;
            if (!string.IsNullOrEmpty(recipientInn) && "0".Equals(recipientInn, StringComparison.Ordinal)) recipientInn = null;

            this.connection.CmdText = QueryAdditionalAgreement(this.connection, dateTransaction, payerAccount);
            object[] records = this.connection.ExecuteReadAllRec();
            if (records == null) return true;

            object[] recordFinded = null;

            // Анализ на БИК+счет+ИНН+номер договора+дата договора
            // Если у данного клиента имеется несколько заявлений о заранее данном акцепте с одинаковыми БИК+счет+ИНН, то анализируются все заявления,
            // пока не находится указанный в назначении платежа номер и дата договора
            foreach (object[] record in records) {
                string bicR = Convert.ToString(record[5]); //БИК
                if (payLocate == 0 && string.IsNullOrEmpty(bicR)) bicR = this.settingBicBank;
                string straccountR = Convert.ToString(record[2]); // Счет зачисления
                string innR = Convert.ToString(record[14]); // ИНН
                if (payLocate == 0 && string.IsNullOrEmpty(innR)) innR = this.settingInnBank;
                if (!string.IsNullOrEmpty(innR) && "0".Equals(innR, StringComparison.Ordinal)) recipientInn = null;
                string contractNumber = Convert.ToString(record[9]); // Номер договора
                DateTime contractDate = Convert.ToDateTime(record[10]); // Дата договора

                if (string.IsNullOrEmpty(bicR) || !bicR.Equals(bicExternalBank, StringComparison.Ordinal)) continue;
                if (string.IsNullOrEmpty(straccountR) || !straccountR.Equals(recipientAccount, StringComparison.Ordinal)) continue;
                if (string.IsNullOrEmpty(innR) || !innR.Equals(recipientInn, StringComparison.Ordinal)) continue;
                if (!CheckAdditionalAgreementDateAndNumber(description, contractNumber, contractDate, out message)) continue;

                recordFinded = record;
                break;
            }

            // Анализ ИНН+Номер договора+дата договора (даже если заполнено одно из полей БИК или счет получателя)
            // Если у данного клиента имеется несколько заявлений о заранее данном акцепте с одинаковыми ИНН, то анализируются все заявления, 
            // пока не находится указанный в назначении платежа номер и дата договора. В случае, если не находится ни одного д/с переходим ко 3-му циклу 
            if (recordFinded == null) {
                foreach (object[] record in records) {
                    string innR = Convert.ToString(record[14]); // ИНН
                    if (payLocate == 0 && string.IsNullOrEmpty(innR)) innR = this.settingInnBank;
                    if (!string.IsNullOrEmpty(innR) && "0".Equals(innR, StringComparison.Ordinal)) recipientInn = null;
                    string contractNumber = Convert.ToString(record[9]); // Номер договора
                    DateTime contractDate = Convert.ToDateTime(record[10]); // Дата договора

                    if (string.IsNullOrEmpty(innR) || !innR.Equals(recipientInn, StringComparison.Ordinal)) continue;
                    if (!CheckAdditionalAgreementDateAndNumber(description, contractNumber, contractDate, out message)) continue;

                    recordFinded = record;
                    break;
                }
            }

            // Универсальный (без БИКа, счета получателя, ИНН, номера и даты договора)
            if (recordFinded == null) {
                foreach (object[] record in records) {
                    string bicR = Convert.ToString(record[5]); //БИК
                    string straccountR = Convert.ToString(record[2]); // Счет зачисления
                    string innR = Convert.ToString(record[14]); // ИНН
                    string contractNumber = Convert.ToString(record[9]); // Номер договора
                    DateTime contractDate = Convert.ToDateTime(record[10]); // Дата договора

                    if (!string.IsNullOrEmpty(bicR)) continue;
                    if (!string.IsNullOrEmpty(straccountR)) continue;
                    if (!string.IsNullOrEmpty(innR)) continue;

                    recordFinded = record;
                    break;
                }
            }

            if (recordFinded == null) return false;

            message = null;
            additionalAgreement = new UbsParam();
            additionalAgreement.Add("Дата дополнительного соглашения", Convert.ToDateTime(recordFinded[1]));
            additionalAgreement.Add("Счет зачисления", Convert.ToString(recordFinded[2]));
            additionalAgreement.Add("Доверенность", Convert.ToInt32(recordFinded[3]));
            additionalAgreement.Add("Наименование организации", Convert.ToString(recordFinded[4]));
            additionalAgreement.Add("БИК", Convert.ToString(recordFinded[5]));
            additionalAgreement.Add("Дата окончания", Convert.ToDateTime(recordFinded[6]));
            additionalAgreement.Add("Номер", Convert.ToString(recordFinded[7]));
            additionalAgreement.Add("Комментарий", Convert.ToString(recordFinded[8]));
            additionalAgreement.Add("Номер договора", Convert.ToString(recordFinded[9]));
            additionalAgreement.Add("Дата договора", Convert.ToDateTime(recordFinded[10]));
            additionalAgreement.Add("Пункт договора", Convert.ToString(recordFinded[11]));
            additionalAgreement.Add("Срок отсрочки оплаты документов", Convert.ToInt32(recordFinded[12]));
            additionalAgreement.Add("Частичное исполнение", Convert.ToInt32(recordFinded[13]));
            additionalAgreement.Add("ИНН", Convert.ToString(recordFinded[14]));
            additionalAgreement.Add("Индекс в массиве", Convert.ToInt32(recordFinded[0]));
            return true;
        }

        private static string QueryAdditionalAgreement(IUbsDbConnection connection, DateTime dateTransaction, string payerAccount) {
            string query =
                "select r0.INDEX_ROW, r0.FIELD_DATE, r1.FIELD_STRING, r2.FIELD_INT, r3.FIELD_STRING, r4.FIELD_STRING, r5.FIELD_DATE, r6.FIELD_STRING, r7.FIELD_STRING, r8.FIELD_STRING, r9.FIELD_DATE, r10.FIELD_STRING, r11.FIELD_INT, r12.FIELD_INT, r13.FIELD_STRING" +
                " from RKO_CONTRACT c" +
                    " inner join OD_ACCOUNTS0 a on a.ID_ACCOUNT = c.ID_ACCOUNT and a.ID_CLIENT > 0 and a.STRACCOUNT = '" + payerAccount + "'" +
                        " and c.DATE_OPEN <= " + connection.sqlDate(dateTransaction) +
                        " and c.DATE_CLOSE > " + connection.sqlDate(dateTransaction) +

                    " inner join RKO_CONTRACT_ADDFL_DIC d on d.NAME_FIELD = 'Доп. соглашение на безакцептное списание'" +

                    // Дата дополнительного соглашения
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r0 on r0.ID_FIELD = d.ID_FIELD and r0.INDEX_COLUMN = 0 and r0.ID_OBJECT = c.ID_CONTRACT" +
                        " and r0.FIELD_DATE <= " + connection.sqlDate(dateTransaction) +
                    // Счет зачисления
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r1 on r1.ID_FIELD = d.ID_FIELD and r1.INDEX_COLUMN = 1 and r1.ID_OBJECT = c.ID_CONTRACT and r1.INDEX_ROW = r0.INDEX_ROW" +
                    // Доверенность
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r2 on r2.ID_FIELD = d.ID_FIELD and r2.INDEX_COLUMN = 2 and r2.ID_OBJECT = c.ID_CONTRACT and r2.INDEX_ROW = r0.INDEX_ROW" +
                    // Наименование организации
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r3 on r3.ID_FIELD = d.ID_FIELD and r3.INDEX_COLUMN = 3 and r3.ID_OBJECT = c.ID_CONTRACT and r3.INDEX_ROW = r0.INDEX_ROW" +
                    // БИК
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r4 on r4.ID_FIELD = d.ID_FIELD and r4.INDEX_COLUMN = 4 and r4.ID_OBJECT = c.ID_CONTRACT and r4.INDEX_ROW = r0.INDEX_ROW" +
                    // Дата окончания
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r5 on r5.ID_FIELD = d.ID_FIELD and r5.INDEX_COLUMN = 5 and r5.ID_OBJECT = c.ID_CONTRACT and r5.INDEX_ROW = r0.INDEX_ROW" +
                        " and r5.FIELD_DATE > " + connection.sqlDate(dateTransaction) +
                    // Номер
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r6 on r6.ID_FIELD = d.ID_FIELD and r6.INDEX_COLUMN = 6 and r6.ID_OBJECT = c.ID_CONTRACT and r6.INDEX_ROW = r0.INDEX_ROW" +
                    // Комментарий
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r7 on r7.ID_FIELD = d.ID_FIELD and r7.INDEX_COLUMN = 7 and r7.ID_OBJECT = c.ID_CONTRACT and r7.INDEX_ROW = r0.INDEX_ROW" +
                    // Номер договора
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r8 on r8.ID_FIELD = d.ID_FIELD and r8.INDEX_COLUMN = 8 and r8.ID_OBJECT = c.ID_CONTRACT and r8.INDEX_ROW = r0.INDEX_ROW" +
                    // Дата договора
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r9 on r9.ID_FIELD = d.ID_FIELD and r9.INDEX_COLUMN = 9 and r9.ID_OBJECT = c.ID_CONTRACT and r9.INDEX_ROW = r0.INDEX_ROW" +
                    // Пункт договора
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r10 on r10.ID_FIELD = d.ID_FIELD and r10.INDEX_COLUMN = 10 and r10.ID_OBJECT = c.ID_CONTRACT and r10.INDEX_ROW = r0.INDEX_ROW" +
                    // Срок отсрочки оплаты документов
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r11 on r11.ID_FIELD = d.ID_FIELD and r11.INDEX_COLUMN = 11 and r11.ID_OBJECT = c.ID_CONTRACT and r11.INDEX_ROW = r0.INDEX_ROW" +
                    // Отказ от обработки при недостат. средств
                    // Частичное исполнение
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r12 on r12.ID_FIELD = d.ID_FIELD and r12.INDEX_COLUMN = 12 and r12.ID_OBJECT = c.ID_CONTRACT and r12.INDEX_ROW = r0.INDEX_ROW" +
                    // ИНН
                    " inner join RKO_CONTRACT_ADDFL_ARRAY r13 on r13.ID_FIELD = d.ID_FIELD and r13.INDEX_COLUMN = 13 and r13.ID_OBJECT = c.ID_CONTRACT and r13.INDEX_ROW = r0.INDEX_ROW";

            return query;
        }

        private static bool CheckAdditionalAgreementDateAndNumber(string description, string contractNumber, DateTime contractDate, out string message) {
            message = null;

            if (string.IsNullOrEmpty(description)) {
                message = "Назначение платежа не указано";
                return false;
            }

            List<string> textContractDates = new List<string>(
                new string[] { contractDate.ToString("dd") + "." + contractDate.ToString("MM") + "." + contractDate.ToString("yyyy"), 
                                       contractDate.ToString("dd") + "." + contractDate.ToString("MM") + "." + contractDate.ToString("yy"),
                                       contractDate.ToString("dd") + "/" + contractDate.ToString("MM") + "/" + contractDate.ToString("yyyy"), 
                                       contractDate.ToString("dd") + "/" + contractDate.ToString("MM") + "/" + contractDate.ToString("yy")
                        });

            description = (description ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
            if (contractDate != new DateTime(2222, 1, 1) && !textContractDates.Exists(m => description.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0)) {
                message = string.Format("В назначении платежа отсутствует дата договора доп. соглашения на безакцептное списание <{0}>", contractDate.ToString("dd.MM.yyyy"));
                return false;
            }

            description = description.Replace("/", string.Empty).Replace("\\", string.Empty);
            contractNumber = (contractNumber ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Replace("/", string.Empty).Replace("\\", string.Empty);

            if (description.IndexOf(contractNumber, StringComparison.OrdinalIgnoreCase) < 0) {
                message = string.Format("В назначении платежа отсутствует номер договора доп. соглашения на безакцептное списание <{0}>", contractNumber);
                return false;
            }

            return true;
        }


        /// <summary>
        /// Проверка отсутствия документов плательщика на второй картотеке
        /// </summary>
        /// <param name="payerAccount">Номер счета плательщика</param>
        /// <param name="priorityPay">Очередность платежа</param>
        /// <param name="datePay">Дата поступления в банк плательщика</param>
        /// <param name="field101">Статус составителя</param>
        /// <param name="extendProtocol">Формировать расширенный протокол</param>
        /// <param name="message">Строка с ошибкой и со списком документов</param>
        /// <returns>true - документы на второй картотеке по счету не найдены, false - документы найдены</returns>
        [Obsolete("Следует использовать метод bool CheckDocumentOnCardIndex2(DateTime operationDate, string payerAccount, byte priorityPay, DateTime datePay, string field101, bool extendProtocol, out string message)", true)]
        public bool CheckDocumentOnCardIndex2(string payerAccount, byte priorityPay, DateTime datePay, string field101, bool extendProtocol, out string message) {
            message = null;
            // в данном методе не хватает даты проводки operationDate
            if (settingTypeCheckDocumentOnCardIndex2 == 0) return true;

            this.connection.ClearParameters();
            this.connection.CmdText =
                "select distinct d2.ID_DOC, isnull(dicd2.FIELD, d2.DATE_DOC), d2.NUM_DOC, d2.STRACCOUNT_DB" +
                ", isnull(d2.STRACCOUNT_R, d2.STRACCOUNT_CR), d2.OBOROT_DB, d2.DATE_TRN" +
                " from OD_DOC_0_ADDFL_DIC dic" +
                    " inner join OD_ACCOUNTS0 a on a.STRACCOUNT = '" + payerAccount + "' and a.ID_CLIENT > 0" +
                        " and dic.NAME_FIELD = 'Дата поступления в банк плательщика'" +
                    " inner join OD_DOC_0_CARD_INDEX cindx on cindx.CARD_INDEX = 2" +
                    " inner join OD_DOC_0 d2 on d2.SET_CART > 0 and cindx.ID_DOC = d2.ID_DOC and d2.OBOROT_DB > 0" +
                    " inner join OD_ACCOUNTS0 a2 on a2.ID_ACCOUNT = d2.ID_ACCOUNT_DB and a2.ID_CLIENT = a.ID_CLIENT" +
                    (settingTypeCheckDocumentOnCardIndex2 == 2 ? " and a2.ID_ACCOUNT = a.ID_ACCOUNT" : string.Empty) +
                    " left outer join OD_DOC_0_ADDFL_DATE dicd2 on dicd2.ID_FIELD = dic.ID_FIELD and dicd2.ID_OBJECT = d2.ID_DOC" +

                    " inner join OD_DOC_0_ADDFL_DIC adc4 on adc4.NAME_FIELD = 'Тип приостановления оплаты'" +
                    " left outer join OD_DOC_0_ADDFL_INT ad4 on ad4.ID_FIELD = adc4.ID_FIELD and ad4.ID_OBJECT = d2.Id_DOC" +

                    " inner join OD_DOC_0_ADDFL_DIC dic3 on dic3.NAME_FIELD = 'Статус составителя расчетного документа'" +
                    " left outer join OD_DOC_0_ADDFL_STRING ads3 on ads3.ID_FIELD = dic3.ID_FIELD and ads3.ID_OBJECT = d2.ID_DOC" +

                " where isnull(ad4.FIELD, 0) = 0" +
                    " and (d2.OCHER_PAYM < " + priorityPay + " or" +
                        " d2.OCHER_PAYM = " + priorityPay + " and isnull(dicd2.FIELD, d2.DATE_DOC) < " + connection.sqlDate(datePay) +
                        " and (isnull(ads3.FIELD, '') <> '' or isnull(ads3.FIELD, '') = '' and '" + field101 + "' = '' ))" +
                " order by 2 asc, 3 asc, 4 asc";

            object[][] records = this.connection.ExecuteReadAllRec2();
            if (records == null) return true;

            StringBuilder builder = new StringBuilder();

            if (settingTypeCheckDocumentOnCardIndex2 == 1)
                builder.AppendLine(string.Format("Обнаружены более приоритетные документы на второй картотеке клиента счета {0}", payerAccount));
            else
                builder.AppendLine(string.Format("По счету плательщика {0} обнаружены более приоритетные документы на второй картотеке", payerAccount));

            if (extendProtocol) {
                builder.AppendLine("--------------------------------------------------------------------------------------------");
                builder.AppendLine("Ид.документа|Дата док. |Номер док.|      Счет ДБ       | Счет получателя/КР |     Сумма     ");
                builder.AppendLine("--------------------------------------------------------------------------------------------");
            }

            UbsODAccount account = new UbsODAccount(this.connection, this.ubs, 0);
            UbsODPayDoc document = new UbsODPayDoc(this.connection, this.ubs, 0);

            bool result = true;
            for (int i = 0; i <= records.GetUpperBound(0); i++) {
                int documentId = Convert.ToInt32(records[i][0]);
                string straccount = Convert.ToString(records[i][3]);
                decimal oborot = Convert.ToDecimal(records[i][5]);
                DateTime operationDate = Convert.ToDateTime(records[i][6]);

                if (straccount != account.StrAccount) account.ReadF(Convert.ToString(records[i][3]));
                document.Read(documentId);

                string message2, bal2R = string.IsNullOrEmpty(document.Account_R) ? document.Account_CR : document.Account_R;
                bal2R = string.IsNullOrEmpty(bal2R) ? null : bal2R.Substring(0, 5);

                if (account.VerifySaldo(operationDate, oborot, 0, document.KindDoc, document.PriorityPay, (string)document.Field("Код бюджетной классификации"), UbsODCheckDocument.GetCashSymbols(document.CashSymbols), bal2R, bal2R, out message2)) {
                    result = false;
                    if (extendProtocol) {
                        builder.AppendLine(
                            string.Format("{0,-12}|{1,-10}|{2,-10}|{3,-20}|{4,-20}|{5,-15}|"
                                , documentId
                                , Convert.ToDateTime(records[i][1]).ToString("dd.MM.yyyy")
                                , records[i][2]
                                , straccount
                                , records[i][4]
                                , oborot.ToString("C", formatCurrency2)
                                ));
                    }
                }
            }

            if (result) return true;
            
            message = builder.ToString();
            return false;
        }

        /// <summary>
        /// Проверка отсутствия документов плательщика на второй картотеке
        /// </summary>
        /// <param name="operationDate">Дата операции</param>
        /// <param name="payerAccount">Номер счета плательщика</param>
        /// <param name="priorityPay">Очередность платежа</param>
        /// <param name="datePay">Дата поступления в банк плательщика</param>
        /// <param name="field101">Статус составителя</param>
        /// <param name="extendProtocol">Формировать расширенный протокол</param>
        /// <param name="message">Строка с ошибкой и со списком документов</param>
        /// <returns>true - документы на второй картотеке по счету не найдены, false - документы найдены</returns>
        public bool CheckDocumentOnCardIndex2(DateTime operationDate, string payerAccount, byte priorityPay, DateTime datePay, string field101, bool extendProtocol, out string message) {
            decimal sum;
            return CheckDocumentOnCardIndex2(operationDate, payerAccount, priorityPay, datePay, field101, extendProtocol, out sum, out message);
        }

        /// <summary>
        /// Проверка отсутствия документов плательщика на второй картотеке
        /// </summary>
        /// <param name="operationDate">Дата операции</param>
        /// <param name="payerAccount">Номер счета плательщика</param>
        /// <param name="priorityPay">Очередность платежа</param>
        /// <param name="datePay">Дата поступления в банк плательщика</param>
        /// <param name="field101">Статус составителя</param>
        /// <param name="extendProtocol">Формировать расширенный протокол</param>
        /// <param name="sum">Общая сумма приоритетных документов</param>
        /// <param name="message">Строка с ошибкой и со списком документов</param>
        /// <returns>true - документы на второй картотеке по счету не найдены, false - документы найдены</returns>
        public bool CheckDocumentOnCardIndex2(DateTime operationDate, string payerAccount, byte priorityPay, DateTime datePay, string field101, bool extendProtocol, out decimal sum, out string message) {
            return CheckDocumentOnCardIndex2(operationDate, payerAccount, priorityPay, datePay, field101, extendProtocol, null, out sum, out message);
        }

        /// <summary>
        /// Проверка отсутствия документов плательщика на второй картотеке
        /// </summary>
        /// <param name="document">дата операции</param>
        /// <param name="extendProtocol">Формировать расширенный протокол</param>
        /// <param name="excludeDocumentIds">Ид. документов исключаемых из оборотов</param>
        /// <param name="sum">Общая сумма приоритетных документов</param>
        /// <param name="message">Строка с ошибкой и со списком документов</param>
        /// <returns>true - документы на второй картотеке по счету не найдены, false - документы найдены</returns>
        public bool CheckDocumentOnCardIndex2(UbsODPayDoc document, bool extendProtocol, int[] excludeDocumentIds, out decimal sum, out string message) {
            string payerAccount = (string.IsNullOrEmpty(document.Account_P) ? document.Account_DB : document.Account_P).Trim();
            string field101 = (string)document.Field("Статус составителя расчетного документа");

            return CheckDocumentOnCardIndex2(document.DateTrn, payerAccount, document.PriorityPay,
                (DateTime)(document.Field("Дата поступления в банк плательщика") ?? dt22220101),
                field101, extendProtocol, excludeDocumentIds, out sum, out message);
        }



        /// <summary>
        /// Проверка отсутствия документов плательщика на второй картотеке
        /// </summary>
        /// <param name="operationDate">дата операции</param>
        /// <param name="payerAccount">Номер счета плательщика</param>
        /// <param name="priorityPay">Очередность платежа</param>
        /// <param name="datePay">Дата поступления в банк плательщика</param>
        /// <param name="field101">Статус составителя</param>
        /// <param name="extendProtocol">Формировать расширенный протокол</param>
        /// <param name="excludeDocumentIds">Ид. документов исключаемых из оборотов</param>
        /// <param name="sum">Общая сумма приоритетных документов</param>
        /// <param name="message">Строка с ошибкой и со списком документов</param>
        /// <returns>true - документы на второй картотеке по счету не найдены, false - документы найдены</returns>
        private bool CheckDocumentOnCardIndex2(DateTime operationDate, string payerAccount, byte priorityPay, DateTime datePay, string field101, bool extendProtocol, int[] excludeDocumentIds, out decimal sum, out string message) {
            sum = 0;
            message = null;
            if (settingTypeCheckDocumentOnCardIndex2 == 0) return true;

            // C картотеки должны списываться документы согласно очередности, а внутри очередности – по календарной дате поступления. И уж внутри даты можно сделать приоритет для налоговых.

            this.connection.ClearParameters();
            this.connection.CmdText =
                "select distinct d2.ID_DOC, isnull(dicd2.FIELD, d2.DATE_DOC), d2.NUM_DOC" +
                    ", d2.STRACCOUNT_DB, isnull(d2.STRACCOUNT_R, d2.STRACCOUNT_CR), d2.OBOROT_DB" +
                    ", d2.OCHER_PAYM, isnull(ads3.FIELD, ''), isnull(a2.IS_BLOK_SUM, 0), d2.ID_ACCOUNT_DB" +
                " from OD_DOC_0_ADDFL_DIC dic" +
                    " inner join OD_ACCOUNTS0 a on a.STRACCOUNT = '" + payerAccount + "' and a.ID_CLIENT > 0" +
                        " and dic.NAME_FIELD = 'Дата поступления в банк плательщика'" +
                    " inner join OD_DOC_0_CARD_INDEX cindx on cindx.CARD_INDEX = 2" +
                    " inner join OD_DOC_0 d2 on d2.SET_CART > 0 and cindx.ID_DOC = d2.ID_DOC and d2.OBOROT_DB > 0" +
                    " inner join OD_ACCOUNTS0 a2 on a2.ID_ACCOUNT = d2.ID_ACCOUNT_DB and a2.ID_CLIENT = a.ID_CLIENT" +
                    (settingTypeCheckDocumentOnCardIndex2 == 2 ? " and a2.ID_ACCOUNT = a.ID_ACCOUNT" : string.Empty) +
                    " left outer join OD_DOC_0_ADDFL_DATE dicd2 on dicd2.ID_FIELD = dic.ID_FIELD and dicd2.ID_OBJECT = d2.ID_DOC" +

                    " inner join OD_DOC_0_ADDFL_DIC adc4 on adc4.NAME_FIELD = 'Тип приостановления оплаты'" +
                    " left outer join OD_DOC_0_ADDFL_INT ad4 on ad4.ID_FIELD = adc4.ID_FIELD and ad4.ID_OBJECT = d2.Id_DOC" +

                    " inner join OD_DOC_0_ADDFL_DIC dic3 on dic3.NAME_FIELD = 'Статус составителя расчетного документа'" +
                    " left outer join OD_DOC_0_ADDFL_STRING ads3 on ads3.ID_FIELD = dic3.ID_FIELD and ads3.ID_OBJECT = d2.ID_DOC" +
                " where isnull(ad4.FIELD, 0) = 0" +
                    " and d2.OCHER_PAYM <= " + priorityPay +
                " order by d2.OCHER_PAYM asc, 2 asc ";

            object[][] records = this.connection.ExecuteReadAllRec2();
            if (records == null) return true;

            StringBuilder builder = new StringBuilder();

            if (settingTypeCheckDocumentOnCardIndex2 == 1)
                builder.AppendLine(string.Format("Обнаружены более приоритетные документы на второй картотеке клиента счета {0}", payerAccount));
            else
                builder.AppendLine(string.Format("По счету плательщика {0} обнаружены более приоритетные документы на второй картотеке", payerAccount));

            if (extendProtocol) {
                builder.AppendLine("--------------------------------------------------------------------------------------------------------");
                builder.AppendLine("Ид.документа|Дата пост.|Номер док.|      Счет ДБ       | Счет получателя/КР |     Сумма     |Оч. |Статус|");
                builder.AppendLine("            |          |          |                    |                    |               |пл. |сост. |");
                builder.AppendLine("--------------------------------------------------------------------------------------------------------");
            }

            Dictionary<int, AccountInfo> saldos = null;

            bool result = true;
            for (int i = 0; i <= records.GetUpperBound(0); i++) {
                int documentId = Convert.ToInt32(records[i][0]);
                DateTime datePayItem = Convert.ToDateTime(records[i][1]);
                string numDoc = Convert.ToString(records[i][2]);
                string straccountDb = Convert.ToString(records[i][3]);
                string straccountCr = Convert.ToString(records[i][4]);
                decimal oborot = Convert.ToDecimal(records[i][5]);
                byte priorityPayItem = Convert.ToByte(records[i][6]);
                string field101Item = Convert.ToString(records[i][7]);

                if (priorityPayItem < priorityPay ||
                    datePayItem < datePay && datePay != dt22220101 ||
                    datePayItem == datePay && datePay != dt22220101 && !string.IsNullOrEmpty(field101Item) && string.IsNullOrEmpty(field101)) {

                    // С учетом очередности, даты поступления, данный документ имеет приоритет в оплате перед проверяемым.
                    // Проверим возможность его списания с картотеки.
                    // Если документ с картотеки можно списать/оплатить частично, то проверка завершается с сообщзением о наличии приоритетных документов
                    // Если документ с картотеки списать/оплатить частично нельзя, то такой документ игнорируется
                    if (saldos == null) saldos = new Dictionary<int, AccountInfo>();
                    if (CanPay(documentId, operationDate, saldos, excludeDocumentIds, connection, ubs)) {
                        result = false;
                        sum += oborot;
                        if (extendProtocol) {
                            builder.AppendLine(
                                string.Format("{0,12}|{1,-10}|{2,10}|{3,-20}|{4,-20}|{5,15}|{6,4}|{7,6}|"
                                    , documentId, datePayItem.ToString("dd.MM.yyyy"), numDoc, straccountDb, straccountCr
                                    , oborot.ToString("C", formatCurrency2), priorityPayItem, field101Item));
                        }
                    }
                }
            }
            message = builder.ToString();
            return result;
        }

        private static bool UseOverdraft(int documentId, IUbsDbConnection connection, IUbsWss ubs) {
            bool useOverdraft = "ДА".Equals((string)ubs.UbsWssParam("Установка", "Операционный день", "Картотека - использ. овердрафт для оплаты"), StringComparison.OrdinalIgnoreCase);
            
            object[] items = (object[])ubs.UbsWssParam("Установка", "Операционный день", "Картотека - гр.опл. овердрафт маски комис.");
            if (items != null) {
                string mask = string.Empty;
                foreach (string item in items) {
                    if (string.IsNullOrEmpty(item)) continue;
                    if (!string.IsNullOrEmpty(mask)) mask += " or";
                    mask += " d.STRACCOUNT_CR like '" + item + "'";
                }

                if (!string.IsNullOrEmpty(mask)) {
                    connection.CmdText = "select case when " + mask + " then 1 else 0 end KMS from OD_DOC_0 d (nolock) where ID_DOC = " + documentId;
                    useOverdraft = useOverdraft && Convert.ToInt32(connection.ExecuteScalar()) == 0;
                }
            }

            return useOverdraft;
        }
        
        



        private static bool CanPay(int documentId, DateTime operationDate, Dictionary<int, AccountInfo> saldos, int[] excludeDocumentIds, IUbsDbConnection connection, IUbsWss ubs) {
            UbsODPayDoc document = new UbsODPayDoc(connection, ubs, 0);
            document.Read(documentId);

            //connection.CmdText = 
            //    "select dnds.OBOROT_DB SUM_NDS from OD_DOC_0_NDS nds" +
            //    " inner join OD_DOC_0 dnds on dnds.ID_DOC = nds.ID_DOC and nds.ID_DOC_PARENT = " + documentId
            //decimal sumDocNDS = Convert.ToDecimal(connection.ExecuteScalar());

            bool useOverdraft = UseOverdraft(documentId, connection, ubs);

            AccountInfo accountInfo;
            if (!saldos.TryGetValue(document.IdAccountDB, out accountInfo)) {
                accountInfo = AccountInfo.Initialize(connection, document.IdAccountDB, operationDate);
                AccountPrognosis prognosis = new AccountPrognosis();
                prognosis.SetAllNeedSum(false);
                prognosis.NeedOborotSum = true;
                prognosis.WhereEndDate = operationDate;
                if (excludeDocumentIds != null) prognosis.WhereIdDocumentNotAre.AddRange(excludeDocumentIds);
                GetAccountPrognosis(connection, ubs, 0, accountInfo.Id, prognosis);
                

                decimal nonTrnOborot = -prognosis.SumOborotDb;
                if (Convert.ToInt32(ubs.UbsWssParam("Установка", "Операционный день", "Картотека - гр.опл. искл.непров. кр оборот")) == 0) nonTrnOborot += prognosis.SumOborotCr;

                // Смещаем остаток на сумму непроведенных документов
                accountInfo.Saldo = UbsOD_GetSaldo.MoveSaldo(accountInfo.Saldo, Convert.ToDecimal(nonTrnOborot), operationDate);

                saldos.Add(document.IdAccountDB, accountInfo);
            }


            VerifyModifySaldo verifyer = new VerifyModifySaldo(connection, ubs);
            string message, bal2R = string.IsNullOrEmpty(document.Account_R) ? document.Account_CR : document.Account_R;
            bal2R = string.IsNullOrEmpty(bal2R) ? null : bal2R.Substring(0, 5);

            decimal availableRest;
            bool result = verifyer.Verify(0, operationDate, 0, false, document.IdAccountDB
                                        , document.Account_DB, accountInfo.State
                                        , accountInfo.Activ, accountInfo.VerLimit
                                        , useOverdraft && accountInfo.VerLimit == 2 /*овердрафт*/ ? 0m : accountInfo.Limit, accountInfo.DateLimit
                                        , accountInfo.IsBlockSum
                                        , accountInfo.IdCurrency, 0, document.KindDoc, document.PriorityPay, (string)document.Field("Код бюджетной классификации")
                                        , GetCashSymbols(document.CashSymbols), bal2R, bal2R, accountInfo.Saldo, out message, out availableRest);
            // Сколько можно списать
            decimal sumDb = result ? (availableRest < document.SummaDB ? availableRest : document.SummaDB) : 0m;

             // Смещаем остаток на сумму документа
            if (sumDb > 0) accountInfo.Saldo = UbsOD_GetSaldo.MoveSaldo(accountInfo.Saldo, -sumDb, operationDate);

            return sumDb > 0;
        }

        /// <summary>
        /// Проверка отсутствия документов плательщика на второй картотеке
        /// </summary>
        /// <param name="payerAccount">Номер счета плательщика</param>
        /// <param name="priorityPay">Очередность платежа</param>
        /// <param name="datePay">Дата поступления в банк плательщика</param>
        /// <param name="extendProtocol">Формировать расширенный протокол</param>
        /// <param name="message">Строка с ошибкой и со списком документов</param>
        /// <returns>true - документы на второй картотеке по счету не найдены, false - документы найдены</returns>
        [Obsolete("Следует использовать перегруженный метод с передачей поля 101 (статуса составителя) и признака формирования расширенного протокола", true)]
        public bool CheckDocumentOnCardIndex2(string payerAccount, byte priorityPay, DateTime datePay, bool extendProtocol, out string message) {
            return CheckDocumentOnCardIndex2(payerAccount, priorityPay, datePay, string.Empty, extendProtocol, out message);
        }

        /// <summary>
        /// Проверка отсутствия документов плательщика на второй картотеке
        /// </summary>
        /// <param name="payerAccount">Номер счета плательщика</param>
        /// <param name="priorityPay">Очередность платежа</param>
        /// <param name="datePay">Дата поступления в банк плательщика</param>
        /// <param name="message">Строка с ошибкой и со списком документов</param>
        /// <returns>true - документы на второй картотеке по счету не найдены, false - документы найдены</returns>
        [Obsolete("Следует использовать перегруженный метод с передачей признак формирования расширенного протокола", true)]
        public bool CheckDocumentOnCardIndex2(string payerAccount, byte priorityPay, DateTime datePay, out string message) {
            return CheckDocumentOnCardIndex2(payerAccount, priorityPay, datePay, true , out message);
        }

        /// <summary>
        /// Проверка отсутствия документов плательщика на второй картотеке
        /// </summary>
        /// <param name="payerAccount">Номер счета плательщика</param>
        /// <returns>true - документы на второй картотеке по счету не найдены, false - документы найдены</returns>
        public bool CheckDocumentOnCardIndex2(string payerAccount) {
            decimal sum1, sum2;
            UbsODCheckDocument.GetSumCardIndex(this.connection, this.ubs, payerAccount, out sum1, out sum2);
            if (sum2 > 0) return false ;

            this.connection.ClearParameters();
            this.connection.CmdText =
                "select count(ID_DOC) from OD_REQ_TO_CURR_ACC where STRACCOUNT_P = '" + payerAccount + "' and NUM_CARD_INDEX > 0 and REST > 0";
            return Convert.ToInt32(this.connection.ExecuteScalar()) == 0;
        }

        /// <summary>
        /// Проверка ключа уникального идентификатора начисления
        /// </summary>
        /// <param name="uin">Уникальный идентификатор начисления</param>
        /// <returns>Результат проверки, true - проверка пройдена</returns>
        public static bool CheckKeyUIN(string uin) {
            int[] v1 = new int[24] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 4 };
            int[] v2 = new int[24] { 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 4, 5, 6 };

            string[] items = new string[] { "0_КФ_KFV", "1_АЛХ_AXGW", "2_БМЦЬ_MIZ", "3_ВНЧЪ_HJ", "4_ГОШ_OL", "5_ДПЩ_N", "6_ЕЁРЭЫ_EPQ", "7_ЖСЮ_CR", "8_ЗТЯ_TS", "9_ИЙУ_YDU" };

            int[] numbers = new int[uin.Length - 1];

            for (int i = 0; i < uin.Length - 1; i++) {
                for(int j = 0; j < items.Length; j++) {
                    if (items[j].IndexOf(string.Empty + uin[i], StringComparison.OrdinalIgnoreCase) >= 0) {
                        numbers[i] = j; break;
                    }
                }
            }


            int sum = 0;
            for (int i = 0; i < numbers.Length; i++) sum += numbers[i] * v1[i];
            int k = sum % 11;

            if (k > 9) {
                sum = 0;
                for (int i = 0; i < numbers.Length; i++) sum += numbers[i] * v2[i];
                k = sum % 11;
                if (k > 9) k = 0;
            }

            return uin.EndsWith(k.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Проверка ключа уникального идентификатора платежа
        /// </summary>
        /// <param name="straccount">Номер счета</param>
        /// <param name="uidPayment">Уникальный идентификатор платежа</param>
        /// <returns>Результат проверки, true - проверка пройдена</returns>
        public static bool CheckKeyUIDPayment(string straccount, string uidPayment) {

            // УИП не может быть 25 нулей
            if ("0000000000000000000000000".Equals(uidPayment, StringComparison.OrdinalIgnoreCase)) return false;

            if (string.IsNullOrEmpty(straccount)) return true;
            if (string.IsNullOrEmpty(uidPayment)) return true;
            if (uidPayment.Length != 25) return false;
            
            int k = 3 * int.Parse(straccount.Substring(0, 1)) +
                    7 * int.Parse(straccount.Substring(1, 1)) +
                    1 * int.Parse(straccount.Substring(2, 1)) +
                    3 * int.Parse(straccount.Substring(3, 1)) +
                    7 * int.Parse(straccount.Substring(4, 1)) +

                    3 * int.Parse(uidPayment.Substring(0, 1)) +
                    7 * int.Parse(uidPayment.Substring(1, 1)) +
                    1 * int.Parse(uidPayment.Substring(2, 1)) +
                    3 * int.Parse(uidPayment.Substring(3, 1)) +
                    7 * int.Parse(uidPayment.Substring(4, 1)) +
                    3 * int.Parse(uidPayment.Substring(5, 1)) +
                    7 * int.Parse(uidPayment.Substring(6, 1)) +
                    1 * int.Parse(uidPayment.Substring(7, 1)) +
                    3 * int.Parse(uidPayment.Substring(8, 1)) +
                    7 * int.Parse(uidPayment.Substring(9, 1)) +
                    3 * int.Parse(uidPayment.Substring(10, 1)) +
                    7 * int.Parse(uidPayment.Substring(11, 1)) +
                    1 * int.Parse(uidPayment.Substring(12, 1)) +
                    3 * int.Parse(uidPayment.Substring(13, 1)) +
                    7 * int.Parse(uidPayment.Substring(14, 1)) +
                    3 * int.Parse(uidPayment.Substring(15, 1));
            return k % 10 == 0;
        }

        /// <summary>
        /// Проверка кредитующихся клиентов
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <returns>true - проверка пройдена</returns>
        public static bool CheckCreditClient(IUbsDbConnection connection, IUbsWss ubs, int documentId) {
            connection.ClearParameters();
            connection.CmdText = 
                "select count(*) from OD_DOC_0 d" +
                    " inner join OD_ACCOUNTS0 a1 on a1.ID_ACCOUNT = d.ID_ACCOUNT_DB and d.ID_DOC = " + documentId +
                    " inner join LOAN_CONTRACT l on l.STATE_CONTRACT = 0 and l.ID_CLIENT > 0 and (l.ID_CLIENT = a1.ID_CLIENT)" +
                    " inner join CLIENTS cl on cl.ID_CLIENT = l.ID_CLIENT and d.PAYM_LOCATE in (0,1) and d.KINDDOC = 1 and (cl.KIND_CLIENT = 1 or cl.KIND_CLIENT = 2 and cl.SIGN_CLIENT = 2)" +
                    " inner join OD_DOC_0_CTRL_DIC cd on cd.SID_CONTROL = 'CREDIT_CLIENT_CONTROL'" +
                        " and not exists(select * from OD_DOC_0_CTRL c" +
                              " where c.ID_DOC = d.ID_DOC and c.KIND_CONTROL = cd.KIND_CONTROL and c.STATE_CONTROL = 0)" +
                        " and exists(select csd.FIELD_STRING from  COM_SETUP_DATA csd" +
                		            " inner join COM_SETUP_SECTION csn on csn.NAME_SECTION = 'Операционный день'" +
                			        " inner join COM_SETUP_SETTING csg on csg.NAME_SETTING = 'Банковские счета и счета вкладов клиентов'" +
                    			        " and csn.ID_SECTION = csg.ID_SECTION and csd.ID_SETTING = csg.ID_SETTING" +
                    			        " and d.STRACCOUNT_DB like csd.FIELD_STRING + '%')";
            return Convert.ToInt32(connection.ExecuteScalar()) == 0;
        }

        /// <summary>
        /// Проверка КПП плательщика
        /// </summary>
        /// <param name="straccount">Номер счета плательщика</param>
        /// <param name="kpp">КПП плательщика</param>
        /// <param name="field101">Статус составителя</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckKppPayer(string straccount, string kpp, string field101, out string message) {
            message = null;

            if(!this.settingCheckKpp) return true;

            kpp = kpp ?? string.Empty;

            // 0000PP000 - P - 0-1 A-Z
            if (!string.IsNullOrEmpty(kpp) && !"0".Equals(kpp, StringComparison.OrdinalIgnoreCase)) {
                if (!Regex.IsMatch(kpp, "\\d{4}[0-9A-Z]{2}\\d{3}")) {
                    message = string.Format("КПП плательщика <{0}> указан неверно", kpp);
                    return false;
                }
            }

            if (!straccount.Equals(this.ubsOdAccount.StrAccount, StringComparison.OrdinalIgnoreCase)) this.ubsOdAccount.ReadF(straccount);
            if (this.ubsOdAccount.IdClient == 0) return true;
            if (this.ubsComClient.Id != this.ubsOdAccount.IdClient) this.ubsComClient.Read(this.ubsOdAccount.IdClient);

            if (this.ubsComClient.Type == 2) {
                if ((!"0".Equals(kpp, StringComparison.OrdinalIgnoreCase) && !"000000000".Equals(kpp, StringComparison.OrdinalIgnoreCase)) &&
                     (!string.IsNullOrEmpty(field101) || string.IsNullOrEmpty(field101) && !string.IsNullOrEmpty(kpp))) {
                    message = string.Format("КПП плательщика (физ. лицо) <{0}> указан неверно, для налогового документа допустимы значения <0> и <000000000>, для не налоговых документов, поле заполняться не должно", kpp); // а если и заполненно то пропустим 0 и 000000000
                    return false;
                }
            }
            else {
                object[] isolatedDivisions = (object[])this.ubsComClient.Field("Обособленные подразделения.КПП");
                bool findInIsolatedDivision = false;
                if (isolatedDivisions != null) {
                    foreach (object[] row in isolatedDivisions)
                        if (kpp.Equals(Convert.ToString(row[1]), StringComparison.OrdinalIgnoreCase)) { findInIsolatedDivision = true; break; }
                }

                if (!"0".Equals(kpp, StringComparison.OrdinalIgnoreCase) && !"000000000".Equals(kpp, StringComparison.OrdinalIgnoreCase) &&
                    !kpp.Equals(this.ubsComClient.KPPU, StringComparison.OrdinalIgnoreCase) && !findInIsolatedDivision &&
                    (!string.IsNullOrEmpty(kpp) || !string.IsNullOrEmpty(field101))) {

                    message = string.Format("КПП плательщика (юр. лицо) <{0}> указан неверно, допустимы значения <0>, <000000000>, КПП карточки клиента и КПП обособленного подразделения", kpp);
                    return false;
                }
                else if (!string.IsNullOrEmpty(kpp) && !"0".Equals(kpp, StringComparison.OrdinalIgnoreCase) && kpp.Length != 9) {
                    message = string.Format("КПП плательщика (юр. лицо) <{0}> указан неверно", kpp);
                    return false;
                }
            }
            return true;            
        }

        /// <summary>
        /// Проверка кода валюты в номере счета
        /// </summary>
        /// <param name="straccount">Номер счета</param>
        /// <param name="codes">Допустимые коды валют</param>
        /// <returns>true - проверка пройдена</returns>
        public static bool CheckAccountCodeCurrency(string straccount, params string[] codes) {
            string codeCurrency = straccount.Substring(5, 3);
            foreach (string code in codes)
                if (code.Equals(codeCurrency, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Поиск по списку закрытых счетов и правоприемников
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="bic">БИК банка</param>
        /// <param name="straccount">Номер счета</param>
        /// <returns>Найденная запись</returns>
        public static object[] SearchAccountInCloseList(IUbsDbConnection connection, IUbsWss ubs, string bic, string straccount) {
            connection.CmdText =
                "select BIC, STRACCOUNT, BIC_SUCCESSOR, STRACCOUNT_SUCCESSOR, DATE_CLOSE" +
                " from COM_DIC_ACC_CLOSE" +
                " where BIC = '" + bic + "' and STRACCOUNT = '" + straccount + "'" +
                " order by BIC asc, STRACCOUNT asc";
            return connection.ExecuteReadFirstRec();
        }

        /// <summary>
        /// Проверка полей платежного ордера
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckPaymentOrder(out string message) {
            message = null;

            if (this.document.KindDoc != 16) return true;
            short numberPartPay = (short)this.document.Field("№ ч. плат.");
            string numberPay = (string)this.document.Field("№ плат.док.");
            DateTime datePay = (DateTime)this.document.Field("Дата плат.док.");
            decimal saldoPay = (decimal)this.document.Field("Сумма ост.пл.");

            if (numberPartPay < 0) {
                message = "Номер частичного платежа должен быть больше или равен 0";
                return false;
            }
            if (datePay == dt22220101) {
                message = "Дата платежного документа не задана";
                return false;
            }
            if (saldoPay < 0) {
                message = "Сумма остатка платежа должна быть больше или равна 0";
                return false;
            }
            if (numberPartPay == 0 && saldoPay > 0) {
                message = "Номер частичного платежа может быть равен нулю только, если сумма остатка платежа равна нулю";
                return false;
            }
            if (numberPartPay == 0 && saldoPay > 0) {
                message = "Номер частичного платежа может быть равен нулю только, если сумма остатка платежа равна нулю";
                return false;
            }
            if (!UbsODCheckDocument.CheckChars(numberPay, allowablePaymentChars, false, out message)) {
                message = "Номер платежного документа содержит недопустимые символы либо не задан" + message;
                return false;
            }

            return true;   
        }
        /// <summary>
        /// Является ли плательщик физ. лицом резидентом
        /// </summary>
        /// <returns>true - плательщик физ. лицо резидент</returns>
        public bool IsPayerResidentFzl() {
            string payerAccount = string.IsNullOrEmpty(this.document.Account_P) ? this.document.Account_DB : this.document.Account_P;

            if (!payerAccount.Equals(this.ubsOdAccount.StrAccount, StringComparison.OrdinalIgnoreCase)) this.ubsOdAccount.ReadF(payerAccount);
            if (this.ubsOdAccount.IdClient > 0) {
                if (this.ubsComClient.Id != this.ubsOdAccount.IdClient) this.ubsComClient.Read(this.ubsOdAccount.IdClient);
                return this.ubsComClient.IsResident && this.ubsComClient.Type == 2 && GetSignClient(this.ubsComClient.Type, this.ubsComClient.Sign, payerAccount) == 1;
            }
            return false;
        }
        /// <summary>
        /// Поиск по списку разрешенных балансовых счетов
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="bal2">Балансовый счет второго порядка</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public static bool CheckApprovedBal2(IUbsDbConnection connection, IUbsWss ubs, string bal2, out string message) {
            message = null;
            connection.CmdText = "select BAL2, ACTIV, IMPL_KO from COM_DIC_BAL2_ALLOW where CB.BAL2='" + bal2 + "'";
            if(connection.ExecuteReadFirstRec() != null) return true;
            message = string.Format("Балансовый счет <{0}> отсутствует в списках разрешенных балансовых счетов", bal2);
            return false;
        }

        /// <summary>
        /// Межрегиональный платеж
        /// </summary>
        /// <param name="bic">БИК банка получателя</param>
        /// <returns>true - платеж межрегиональный</returns>
        public bool IsInnterRegionPayment(string bic) {
            return this.settingСodesOfTheRegions.Contains(bic.Substring(2, 2));
        }

        /// <summary>
        /// Проверка вида отправки платежа 
        /// </summary>
        /// <param name="typeSend">Вид отправки платежа</param>
        /// <param name="kinddoc">Шифр документа ЦБ</param>
        /// <param name="bic">БИК банка получателя</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckTypeSend(byte typeSend, byte kinddoc, string bic, out string message) {
            message = null;
            if (typeSend == 100) typeSend = 0;
            if (typeSend == 3) return true; // Срочно

            bool innterregionPayment = IsInnterRegionPayment(bic);

            connection.ClearParameters();
            connection.CmdText = 
                "select isnull(b.INTER_REP, 0), isnull(b.INTRA_REP, 0), t.COD_TYPE" +
                " from COM_DIC_BANK b, COM_DIC_TYPE_BANK t" +
                " where b.ID_TYPE_BANK = t.ID_TYPE and b.BIC = '" + bic + "'";
            object[] record = connection.ExecuteReadFirstRec();
            byte intER = Convert.ToByte(record[0]); // Участник ВЭП
            byte intRA = Convert.ToByte(record[1]); // Участник МЭП
            string code = Convert.ToString(record[2]);
            
            // Если банк получателя платежа ПУ и параметр вид операции существует и вид операции 0 и (банк получателя находится в регионе банка РЦ
            // и банк получателя является участником ВЭП) или (банк получателя не находится в регионе банка РЦ и банк получателя является участником МЭП))
            if ("40".Equals(code, StringComparison.OrdinalIgnoreCase) && kinddoc == 1 && ( !innterregionPayment && intRA == 1 || innterregionPayment && intER == 1 )) {
                // вид платежа только электронно
                if (typeSend != 0) {
                    message = "Вид отправки платежа должен быть <электронно>";
                    return false;
                }
            }

            // Если банк получателя находится в регионе банка РЦ и банк получателя не является участником ВЭП или МЭП
            // или банк получателя не находится в регионе банка РЦ и банк получателя не является участником МЭП
            if( !innterregionPayment && intER == 0 && intRA == 0 || innterregionPayment && intER == 0 ) {
                // запрещено электронно, только только почта/телеграф
                if (typeSend == 0) {
                    message = "Вид отправки платежа должен быть <почта/телеграф>";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// РЦ получить активный субформат
        /// </summary>
        /// <returns>Активный субформат</returns>
        public string GetSubFormatRc() {
            this.connection.ClearParameters();
            this.connection.CmdText = 
                "select c.ID_CONTRACT, f.SID_FORMAT" +
                " from RC_CONTRACT c, RC_MSG_FORMAT f" +
                " where c.ACTIVE_FORMAT = f.ID_FORMAT and c.CODE = '" + this.settingCodeRKC + "'";
            object[] record = this.connection.ExecuteReadFirstRec();
            if (record == null) return null;
            int contractId = Convert.ToInt32(record[0]);
            string format = Convert.ToString(record[1]);
            if ("UBS_CB".Equals(format, StringComparison.OrdinalIgnoreCase)) {
                this.connection.CmdText =
                    "select f.FIELD_STRING from RC_CONTRACT_ADDFL_DIC d, RC_CONTRACT_ADDFL f" +
                    " where d.NAME_FIELD = 'Активный субформат' and d.ID_FIELD = f.ID_FIELD and f.ID_OBJECT = " + contractId;
                format = (string)this.connection.ExecuteScalar();
            }
            return format;
        }

        /// <summary>
        /// РЦ проверка банка по справочнику БЭСП
        /// </summary>
        /// <param name="bic">Бик банка получателя</param>
        /// <param name="accountExtBank">Корр. счет банка получателя</param>
        /// <param name="recipientAccount">Номер счета получателя</param>
        /// <param name="transactionDate">Дата проведения</param>
        /// <param name="typeSend">Вид отправки документа</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckBankBesp(string bic, string accountExtBank, string recipientAccount, DateTime transactionDate, byte typeSend, out string message) {
            message = null;

            string format = GetSubFormatRc();
            if (string.IsNullOrEmpty(format)) return true;

            if (typeSend != 3) return true;

            if ("1.1".Equals(format, StringComparison.OrdinalIgnoreCase) || "2.1".Equals(format, StringComparison.OrdinalIgnoreCase)) {
                return CheckBankBespFormat1(bic, accountExtBank, recipientAccount, transactionDate, out message);
            }
            else if ("2.2".Equals(format, StringComparison.OrdinalIgnoreCase) || "2.3".Equals(format, StringComparison.OrdinalIgnoreCase) || "2.4".Equals(format, StringComparison.OrdinalIgnoreCase)) {
                return CheckBankBespFormat2(bic, accountExtBank, recipientAccount, transactionDate, out message);
            }
            else {
                message = string.Format("РЦ субформат <{0}> не поддерживается", format);
                return false;
            }
        }
        private bool CheckBankBespFormat1(string bic, string accountExtBank, string recipientAccount, DateTime transactionDate, out string message) {
            message = null;

            string searchAccount = (string.IsNullOrEmpty(accountExtBank) || "00000000000000000000".Equals(accountExtBank, StringComparison.OrdinalIgnoreCase)) ? recipientAccount : accountExtBank;
            string uis = bic.Substring(2) + "000";            
            
            string registrationDate = this.connection.sqlDate(transactionDate.Date.AddDays(1));

            // Поиск банка
            // 1 ОУР
            this.connection.ClearParameters();
            this.connection.CmdText =
                "select ID_BANK from RC_DIC_BANK_BESP" +
                " where OURBIC = '" + bic + "'" +
                    " and BIC is NULL" +
                    " and UIS is NULL" +
                    " and FLAG > 0" +
                    " and MemberType = 3" +
                    " and RegistrationMode = 1" +
                    " and RegistrationDate < " + registrationDate +
                    " and isnull(DisconnectionMode, 1) in (1,3)" +
                    " and isnull(StoppageMode,1) = 1";
            int bankId = Convert.ToInt32(this.connection.ExecuteScalar());

            if (bankId == 0) {
                // 2 ПУР
                this.connection.CmdText =
                    "select ID_BANK from RC_DIC_BANK_BESP" +
                    " where BIC = '" + bic + "'" +
                        " and FLAG > 0" +
                        " and MemberType = 2" +
                        " and RegistrationMode = 1" +
                        " and RegistrationDate < " + registrationDate +
                        " and isnull(DisconnectionMode, 1) in (1,3)" +
                        " and isnull(StoppageMode, 1) = 1";
                bankId = Convert.ToInt32(this.connection.ExecuteScalar());
            }
            if (bankId == 0) {
                // 3 АУР
                this.connection.CmdText =
                    "select ID_BANK from RC_DIC_BANK_BESP" +
                    " where UIS = '" + uis + "'" +
                        " and FLAG > 0" +
                        " and (MemberType = 1 or MemberType = 6)" +
                        " and RegistrationMode = 1" +
                        " and RegistrationDate < " + registrationDate +
                        " and isnull(DisconnectionMode, 1) in (1,3)" +
                        " and isnull(StoppageMode, 1) = 1";
                bankId = Convert.ToInt32(this.connection.ExecuteScalar());
            }

            if (bankId == 0) {
                message = string.Format("БИК <{0}> не найден в справочнике банков БЭСП среди банков, расчеты с которыми разрешены на данный момент", bic);
                return false;
            }
            
            // Поиск счета
            this.connection.CmdText =
                "select ID_BANK from RC_DIC_BANK_BESP_ACC" +
                " where ID_BANK = " + bankId + 
                    " and ACC = '" + searchAccount + "'";
            if(this.connection.ExecuteScalar() == null) {
                message = string.Format("Счет <{0}> не найден в справочнике банков БЭСП среди счетов банков, расчеты с которыми разрешены на данный момент", searchAccount);
                return false;
            }

            return true;
        }
        private bool CheckBankBespFormat2(string bic, string accountExtBank, string recipientAccount, DateTime transactionDate, out string message) {
            message = null;

            string searchAccount = (string.IsNullOrEmpty(accountExtBank) || "00000000000000000000".Equals(accountExtBank, StringComparison.OrdinalIgnoreCase)) ? recipientAccount : accountExtBank;
            string recipientAccountBal2 = recipientAccount.Substring(0, 5);
            string registrationDate = this.connection.sqlDate(transactionDate.Date.AddDays(1));

            int bankId = 0, isour = 0;
            //short isUrgent = 0;

            // Поиск банка
            // 1 ОУР
            this.connection.ClearParameters();
            this.connection.CmdText =
                "select ID_BANK, isnull(UrgentPayments, 0) from RC_DIC_BANK_BESP" +
                " where OURBIC = '" + bic + "'" +
                    " and BIC is NULL" +
                    " and UIS is NULL" +
                    " and FLAG > 0" +
                    " and MemberType = 3" +
                    " and RegistrationMode = 1" +
                    " and RegistrationDate < " + registrationDate +
                    " and isnull(StoppageMode, 1) in (1,3)";
            object[] record = this.connection.ExecuteReadFirstRec();

            if (record != null) {
                bankId = Convert.ToInt32(record[0]);
                //isUrgent = Convert.ToInt16(record[1]);
      
                // 4 если есть ОУР проверить нет-ли по нему АУР не КО
                this.connection.CmdText =
                    "select ID_BANK, isnull(UrgentPayments, 0) from RC_DIC_BANK_BESP" +
                    " where OURBIC = '" + bic + "'" +
                        " and BIC is NULL" +
                        " and UIS is not NULL" +
                        " and FLAG > 0" +
                        " and (MemberType = 1 or MemberType = 6)" +
                        " and RegistrationMode = 1" +
                        " and RegistrationDate < " + registrationDate +
                        " and isnull(StoppageMode, 1) in (1,3)";
                if(this.connection.ExecuteReadFirstRec() != null) isour = 2; // если это АУР и не КО
            }
            else {
                // 2 ПУР и АУР КО
                this.connection.CmdText =
                    "select ID_BANK, isnull(UrgentPayments, 0) from RC_DIC_BANK_BESP" +
                    " where BIC = '" + bic + "'" +
                        " and FLAG > 0" +
                        " and MemberType in (2, 1, 6)" +
                        " and RegistrationMode = 1" +
                        " and isnull(StoppageMode, 1) in (1, 3)" +
                        " and RegistrationDate < " + registrationDate +
                        " and (isnull(StoppageDate, " + this.connection.sqlDate(dt22220101) + ") >= " + registrationDate +
                            " or isnull(StoppageEndDate, " + this.connection.sqlDate(dt22220101) + ") < " + registrationDate + ")";
                record = this.connection.ExecuteReadFirstRec();
                if (record != null) {
                    bankId = Convert.ToInt32(record[0]);
                    //isUrgent = Convert.ToInt16(record[1]);
                }
                else {
                    // 1 АУР не КО
                    this.connection.CmdText =
                        "select ID_BANK, isnull(UrgentPayments, 0) from RC_DIC_BANK_BESP" +
                        " where OURBIC = '" + bic + "'" +
                            " and BIC is NULL" +
                            " and UIS is not NULL" +
                            " and FLAG > 0" +
                            " and (MemberType = 1 or MemberType = 6)" +
                            " and RegistrationMode = 1" +
                            " and RegistrationDate < " + registrationDate +
                            " and isnull(StoppageMode, 1) in (1, 3)";
                    record = this.connection.ExecuteReadFirstRec();
                    if (record != null) {
                        bankId = Convert.ToInt32(record[0]);
                        //isUrgent = Convert.ToInt16(record[1]);
                        isour = 1; // если это АУР и не КО
                    }
                }
            }
           
            if (bankId == 0) {
                message = string.Format("БИК <{0}> не найден в справочнике банков БЭСП среди банков, расчеты с которыми разрешены на данный момент", bic);
                return false;
            }

            // Поиск счета
            switch(isour) {
                case 2:
                    this.connection.CmdText = 
                        "select acc.ACC, bnk.UIS, isnull(bnk.UrgentPayments, 0)" +
                        " from RC_DIC_BANK_BESP_ACC acc, RC_DIC_BANK_BESP bnk" +
                        " where Acc.id_bank = bnk.id_bank" +
                            " and bnk.OURBIC = '" + bic + "'" +
                            " and bnk.BIC is NULL" +
                            " and bnk.FLAG > 0" +
                            " and (bnk.MemberType = 1 or bnk.MemberType = 6)" +
                            " and bnk.RegistrationMode = 1" +
                            " and bnk.RegistrationDate < " + registrationDate +
                            " and isnull(bnk.StoppageMode, 1) in (1,3)" +
                        " UNION " +
                        " select ACCBRF, '', 0 from RC_DIC_BANK_BESP_ACCBRF" +
                        " where ID_BANK = " + bankId;
                    break;
                case 1:
                    this.connection.CmdText = 
                    "select acc.ACC, bnk.UIS, isnull(bnk.UrgentPayments, 0)" +
                    " from RC_DIC_BANK_BESP_ACC acc, RC_DIC_BANK_BESP bnk" +
                    " where Acc.id_bank = bnk.id_bank" +
                        " and bnk.OURBIC = '" + bic + "'" +
                        " and bnk.BIC is NULL" +
                        " and bnk.UIS is not NULL" +
                        " and bnk.FLAG > 0" +
                        " and (bnk.MemberType = 1 or bnk.MemberType = 6)" +
                        " and bnk.RegistrationMode = 1" +
                        " and bnk.RegistrationDate < " + registrationDate +
                        " and isnull(bnk.StoppageMode, 1) in (1, 3)";
                    break;
                default:
                    this.connection.CmdText = 
                        "select ACCBRF, '', 0 from RC_DIC_BANK_BESP_ACCBRF" +
                        " where ID_BANK = " + bankId;
                    break;
            }
            
            //string info = null;
            this.connection.ExecuteUbsDbReader();
            while(this.connection.Read()) {
                string account = this.connection.GetString(0);
                if (searchAccount.Equals(account, StringComparison.OrdinalIgnoreCase)) {
                    //if (isour == 1) {
                    //    isUrgent = connection.GetInt32(2);
                    //    info = string.Format("(UIS - {0})", this.connection.GetValue(1));
                    //}
                    return true;
                }
                if (isour != 1 && recipientAccountBal2.Equals(account, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            message = string.Format("Счет <{0}/{1}> по БИКу <{2}> не найден в справочнике банков БЭСП среди счетов банков, расчеты с которыми разрешены на данный момент ({3})", recipientAccountBal2, recipientAccount, bic, isour);
            return false;
        }

        /// <summary>
        /// РЦ Проверка дубликаитов платежей в течении 10 дней
        /// </summary>
        /// <param name="number">Номер документа</param>
        /// <param name="bicExtBank">БИК внешнего банка</param>
        /// <param name="documentDate">Дата документа</param>
        /// <param name="transactionDate">Дата проведения документа</param>
        /// <param name="payerAccount">Номер счета плательщика</param>
        /// <param name="recipientAccount">Номер счета получателя</param>
        /// <param name="oborot">Сумма списания</param>
        /// <returns>true - проверка пройдена</returns>
        public bool CheckDuplicatePayments(string number, string bicExtBank, DateTime documentDate, DateTime transactionDate, string payerAccount, string recipientAccount, decimal oborot) {
            // Уникальность следующих реквизитов документа в течение десяти календарных дней, не считая дня их выписки
            if ("00000".Equals(this.settingSearchDuplicatePayment, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            else if ("11111".Equals(this.settingSearchDuplicatePayment, StringComparison.OrdinalIgnoreCase)) { // поиск по всем направлениям (как было раньше)
                this.connection.ClearParameters();
                this.connection.CmdText =
                    "select 1" +
                    " from RC_PAYMENT_BASE_CUBE RCPB_C" +
                    " where RCPB_C.NDOC = '" + number + "'" +
                        " and RCPB_C.BIC_BANK_BEN = '" + bicExtBank + "'" +
                        " and RCPB_C.ID_ERROR = 0" +
                        " and RCPB_C.TYPE_OPER not in (3, 5, 7, 8, 9)" +
                        " and RCPB_C.DPP >= " + this.connection.sqlDate(transactionDate.AddDays(-10)) +
                        " and RCPB_C.DPP <= " + this.connection.sqlDate(transactionDate) +
                        " and RCPB_C.BIC_BANK_PAYER = '" + this.settingBicBank + "'" +
                        " and RCPB_C.DATE_DOC = " + this.connection.sqlDate(documentDate) +
                        " and RCPB_C.STRACCOUNT_PAYER = '" + payerAccount + "'" +
                        " and RCPB_C.ACC_BEN = '" + recipientAccount + "'" +
                        " and RCPB_C.SUMMA_PAYMENT = " + this.connection.sqlDecimal(oborot);
                return Convert.ToInt32(this.connection.ExecuteScalar()) == 0;
            }
            else {
                List<string> parameters = new List<string>();

                if (this.settingSearchDuplicatePayment[0] == '1') parameters.Add("0"); // 0 - неопределен
                if (this.settingSearchDuplicatePayment[1] == '1') parameters.Add("1"); // 1 - входящий
                if (this.settingSearchDuplicatePayment[2] == '1') parameters.Add("2"); // 2 - исходящий
                if (this.settingSearchDuplicatePayment[3] == '1') parameters.Add("3"); // 3 - транзитный
                if (this.settingSearchDuplicatePayment[4] == '1') parameters.Add("4"); // 4 - внутренний

                this.connection.ClearParameters();
                this.connection.CmdText =
                    "select 1" +
                    " from RC_PAYMENT_BASE_CUBE RCPB_C, RC_PAYMENT" +
                    " where RCPB_C.NDOC = '" + number + "'" +
                        " and RCPB_C.BIC_BANK_BEN = '" + bicExtBank + "'" +
                        " and RCPB_C.ID_ERROR = 0" +
                        " and RCPB_C.TYPE_OPER not in (3, 5, 7, 8, 9)" +
                        " and RCPB_C.DPP >= " + this.connection.sqlDate(transactionDate.AddDays(-10)) +
                        " and RCPB_C.DPP <= " + this.connection.sqlDate(transactionDate) +
                        " and RCPB_C.BIC_BANK_PAYER = '" + this.settingBicBank + "'" +
                        " and RCPB_C.DATE_DOC = " + this.connection.sqlDate(documentDate) +
                        " and RCPB_C.STRACCOUNT_PAYER = '" + payerAccount + "'" +
                        " and RCPB_C.ACC_BEN = '" + recipientAccount + "'" +
                        " and RCPB_C.SUMMA_PAYMENT = " + this.connection.sqlDecimal(oborot) +
                        " and RC_PAYMENT.ID_PAYMENT = RCPB_C.ID_PAYMENT" +
                            (parameters.Count > 0 ? " and RC_PAYMENT.DIRECTION in (" + string.Join(",", parameters.ToArray()) + ") " :  string.Empty);
                return Convert.ToInt32(this.connection.ExecuteScalar()) == 0;
            }
        }

        /// <summary>
        /// РЦ Проверка создан ли платеж по документу
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>true - Проверка пройдена</returns>
        public static bool CheckPaymentCreated(IUbsDbConnection connection, IUbsWss ubs, int documentId, out string message) {
            message = null;
            connection.CmdText = 
                "select PAYMENT.ID_PAYMENT" +
                " from RC_PAYMENT PAYMENT" +
                " where PAYMENT.ID_SOURCE = " + documentId +
                    " and PAYMENT.SOURCE_TYPE = 2";
            int paymentId = Convert.ToInt32(connection.ExecuteScalar());
            if (paymentId > 0) {
                message = string.Format("По документу ид. <{0}> был создан платеж ид. <{1}>", documentId, paymentId); 
                return false;
            }
            return true;
        }

        /// <summary>
        /// Проверка наличия документа в списке документов отмеченных в террористической деятельности (проверка обязательного контроля).
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <returns>true - проверка пройдена, документ в списке отсутствует</returns>
        public static bool CheckMarkTerroristActivities(IUbsDbConnection connection, IUbsWss ubs, int documentId) {
            return GetMarkTerroristActivities(connection, ubs, documentId) <= 0;
        }

        /// <summary>
        /// Проверка наличия документа в списке документов отмеченных в террористической деятельности (проверка обязательного контроля).
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <returns>-1 - документ не ставился на обязателльный контроль, 0 - обязательный контроль пройден, 1 - обязательный контроль не пройден</returns>
        public static int GetMarkTerroristActivities(IUbsDbConnection connection, IUbsWss ubs, int documentId) {
            connection.CmdText = "select t.IS_TERRORIST from OD_DOC_0_CHECK_TERROR t where t.ID_DOC = " + documentId;
            object scalar = connection.ExecuteScalar();
            if (scalar == null) return -1;
            return Convert.ToInt32(scalar);
        }

        /// <summary>
        /// Установить отметку требования прохождения обязательного контроля (подозрение в террористической деятельности) для документа. 
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        public static void SetMarkTerroristActivities(IUbsDbConnection connection, IUbsWss ubs, int documentId) {
            SetMarkTerroristActivities(connection, ubs, documentId, true);
        }
        /// <summary>
        /// Установить/снять отметку требования прохождения обязательного контроля (подозрение в террористической деятельности) для документа. 
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <param name="setMark">Признак установки отметки (true - установить/false - снять)</param>
        public static void SetMarkTerroristActivities(IUbsDbConnection connection, IUbsWss ubs, int documentId, bool setMark) {
            connection.ClearParameters();
            if (setMark) {
                connection.CmdText =
                    "insert into OD_DOC_0_CHECK_TERROR (ID_DOC, IS_TERRORIST)" +
                        " select " + documentId + ", 1 where not exists(select * from OD_DOC_0_CHECK_TERROR t where t.ID_DOC = " + documentId + ")";
            }
            else {
                connection.CmdText = "update OD_DOC_0_CHECK_TERROR set IS_TERRORIST = 0 where ID_DOC = " + documentId;
            }
            connection.ExecuteNonQuery();
        }

        /// <summary>
        /// Проверка полей документа в подозрении на террористическую деятельность
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <returns>Возвращает true - проверка пройдена</returns>
        public bool CheckTerroristActivities(out string message) {
            List<string> checkingFields = new List<string>();

            message = null;

            if (this.settingCheckTerroristActivitesPayerName) {
                string payerAccount = string.IsNullOrEmpty(this.document.Account_P) ? this.document.Account_DB : this.document.Account_P;
                if (payerAccount.Equals(this.ubsOdAccount.StrAccount, StringComparison.OrdinalIgnoreCase)) this.ubsOdAccount.ReadF(payerAccount);
                if (this.ubsOdAccount.IdClient > 0) {
                    if (this.ubsComClient.Id != this.ubsOdAccount.IdClient) this.ubsComClient.Read(this.ubsOdAccount.IdClient);
                    if (!ubsComLibrary.CheckTerroristActivities(this.ubsComClient, out message)) return false;
                }
                else {
                    checkingFields.Add(this.document.Name_P);
                }
            }
            if (this.settingCheckTerroristActivitesRecipientName) {
                if (this.document.PayLocate != 0) { // Для исходящих документов клиента искать не нужно
                    checkingFields.Add(this.document.Name_R);
                }
                else {
                    string recipientAccount = string.IsNullOrEmpty(this.document.Account_R) ? this.document.Account_CR : this.document.Account_R;
                    if (recipientAccount.Equals(this.ubsOdAccount.StrAccount, StringComparison.OrdinalIgnoreCase)) this.ubsOdAccount.ReadF(recipientAccount);
                    if (this.ubsOdAccount.Id > 0 && this.ubsOdAccount.IdClient > 0) {
                        if (this.ubsComClient.Id != this.ubsOdAccount.IdClient) this.ubsComClient.Read(this.ubsOdAccount.IdClient);
                        if (!ubsComLibrary.CheckTerroristActivities(this.ubsComClient, out message)) return false;
                    }
                    else {
                        checkingFields.Add(this.document.Name_R);
                    }
                }
            }
            
            if (this.settingCheckTerroristActivitesNote) checkingFields.Add(this.document.Description);
            if (this.settingCheckTerroristActivitesConditionPay) checkingFields.Add(this.document.ConditionPay);
            // if (this.settingCheckTerroristActivitesBankPayer) checkingFields.Add(this.document); // банк плательщика проверять бессмысленно
            if (this.settingCheckTerroristActivitesBankRecipient && document.PayLocate == 1) checkingFields.Add(this.document.NameExtBank);

            if (checkingFields.Count > 0) {
                if (!ubsComLibrary.CheckTerroristActivities(checkingFields.ToArray(), out message)) return false;
            }
            return true;
        }

         /// <summary>
        /// Проверка документа прохождения ручного контроля
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <returns>true - проверка пройдена, документ прошел ручной контроль или контроль не требуется</returns>
        public static bool CheckManualControl(IUbsDbConnection connection, IUbsWss ubs, int documentId) {
            connection.ClearParameters();
            connection.CmdText = "select 1 from OD_DOC_0_MANUAL_CTRL m where m.STATE = 1 and m.ID_DOC = " + documentId;
            return Convert.ToInt32(connection.ExecuteScalar()) == 0;
        }

        /// <summary>
        /// Постановка документа на ручной контроль
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <param name="comment">Причина постановки на ручной контроль</param>
        public static void SetManualControl(IUbsDbConnection connection, IUbsWss ubs, int documentId, string comment) {
            connection.CmdText =
                "insert into OD_DOC_0_MANUAL_CTRL (ID_DOC, STATE, NOTE, ID_USER_CREATE, TIME_CREATE, ID_USER_EDIT, TIME_EDIT)" +
                    " select " + documentId + ", 1, '" + comment.Replace("'", "''") + "', " + ubs.UbsWssParam("IdUser") + ", getdate(), NULL, NULL" +
                    " where not exists (select ID_DOC from OD_DOC_0_MANUAL_CTRL where ID_DOC = " + documentId + ")";
            connection.ExecuteNonQuery();
        }

        /// <summary>
        /// Проверить требуется ли контроль проводок между транзитными и текущими счетами
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <returns>true - проверка пройдена документ не требует контроля проводки между транзитным и текущим счетом</returns>
        public static bool CheckTransitDocumentControl(IUbsDbConnection connection, IUbsWss ubs, int documentId) {
            connection.ClearParameters();
            connection.CmdText = 
                "select isnull(s1.SENSE_STRING, ' '), isnull(s2.SENSE_STRING, ' ') from OD_DOC_0 d" +
                    " inner join OD_ACC0_ADDFL_DIC a on a.NAME_FIELD = 'Цель счёта' and d.ID_DOC = " + documentId +
                    " inner join OD_ACCOUNTS0 a1 on a1.ID_ACCOUNT = d.ID_ACCOUNT_DB" +
                    " inner join OD_ACCOUNTS0 a2 on a2.ID_ACCOUNT = d.ID_ACCOUNT_CR and a1.NUMBRANCH = a2.NUMBRANCH" +
                    " left outer join OD_ACC0_ADDFL_INT f1 on f1.ID_FIELD = a.ID_FIELD and f1.ID_OBJECT = d.ID_ACCOUNT_DB" +
                    " left outer join OD_ACC0_ADDFL_INT f2 on f2.ID_FIELD = a.ID_FIELD and f2.ID_OBJECT = d.ID_ACCOUNT_CR" +
                    " left outer join OD_ACC0_ADDFL_SENSE s1 on s1.SENSE_INT = f1.FIELD and s1.ID_FIELD = f1.ID_FIELD" +
                    " left outer join OD_ACC0_ADDFL_SENSE s2 on s2.SENSE_INT = f2.FIELD and s2.ID_FIELD = f2.ID_FIELD";
            object[] record = connection.ExecuteReadFirstRec();
            if (record != null) {

            string senseDb = Convert.ToString(record[0]).Trim();
            string senseCr = Convert.ToString(record[1]).Trim();

            if ("ТЕКУЩИЙ".Equals(senseDb, StringComparison.OrdinalIgnoreCase) && "ТРАНЗИТНЫЙ".Equals(senseCr, StringComparison.OrdinalIgnoreCase) ||
                "ТЕКУЩИЙ".Equals(senseCr, StringComparison.OrdinalIgnoreCase) && "ТРАНЗИТНЫЙ".Equals(senseDb, StringComparison.OrdinalIgnoreCase)) return false; 
            }
            return true;
        }

        /// <summary>
        /// Получить признак добавления ордера в табличный ордер
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="documentId">Идентификатор документа</param>
        /// <param name="razdel">Раздел баланса</param>
        /// <returns>true - ордер в составе табличного ордера</returns>
        public static bool IsDocumentAddInTableOrder(IUbsDbConnection connection, IUbsWss ubs, int documentId, byte razdel) {
            connection.ClearParameters();
            connection.CmdText = "select 1 from OD_TABLE_ORDER_" + razdel + "_DOC where ID_DOC = " + documentId;
            return Convert.ToInt32(connection.ExecuteScalar()) > 0;
        }

        private static string[] GetCashSymbols(object cashSymbols) {
            object[] symbols = (object[])cashSymbols;
            if (symbols == null) return null;

            List<string> list = new List<string>();
            foreach (object[] item in symbols) list.Add((string)item[0]);
            return list.ToArray();
        }

        /// <summary>
        /// Определить необходимость прохождения ручного контроля для документа раздела А
        /// </summary>
        /// <param name="connection">Интерфейс взаимодействия с БД</param>
        /// <param name="ubs">Интерфейс взаимодействия с сервером приложений</param>
        /// <param name="document">Документ раздела А</param>
        /// <param name="operationDate">Дата операции</param>
        /// <returns>true - Документ следует поставить на ручной контроль</returns>
        public static bool IsNeedManualControl(IUbsDbConnection connection, IUbsWss ubs, UbsODPayDoc document, DateTime operationDate) {
            short kinddoc = document.KindDoc;
            byte priorityPay = document.PriorityPay;
            string bal2DB = document.Account_DB.Substring(0, 5);
            string bal2CR = (string.IsNullOrEmpty(document.Account_R) ? document.Account_CR : document.Account_R).Substring(0, 5);
            decimal oborotDb = document.SummaDB;
            string currencyCode = document.Account_DB.Substring(5, 3);

            // Пересчет валютной суммы в рубли на дату операции по курсу ЦБ
            if (!"643".Equals(currencyCode, StringComparison.OrdinalIgnoreCase) && !"810".Equals(currencyCode, StringComparison.OrdinalIgnoreCase)) {
                UbsComCurrency currency = new UbsComCurrency(connection, ubs);
                UbsComRates rates = new UbsComRates(connection, ubs);
                currency.Find_CB(currencyCode);
                decimal rate;
                int nu;
                rates.GetRateCB(currency.Id_Currency, operationDate, out rate, out nu);
                oborotDb = oborotDb * rate / (decimal)nu;
            }
            
            object[] items = (object[])ubs.UbsWssParam("Получить установку", "Операционный день", "Ручной контроль");
            foreach (object[] item in items) {
                // По минимальной сумме в рублях
                if (Convert.ToDecimal(item[4]) != 0 && oborotDb < Convert.ToDecimal(item[4])) continue;
                // По балансовому счету получателя/кредит 1 или 2-го порядка
                string value = Convert.ToString(item[3]);
                if (!Regex.IsMatch(value, string.Format("\\b{0}\\b", bal2CR)) &&
                    !Regex.IsMatch(value, string.Format("\\b{0}\\b", bal2CR.Substring(0, 3))) &&
                    !value.Contains("*")) continue;
                // По балансовому счету дебет 1 или 2-го порядка
                value = Convert.ToString(item[2]);
                if (!Regex.IsMatch(value, string.Format("\\b{0}\\b", bal2DB)) &&
                    !Regex.IsMatch(value, string.Format("\\b{0}\\b", bal2DB.Substring(0, 3))) &&
                    !value.Contains("*")) continue;
                // По очередности платежа
                value = Convert.ToString(item[1]);
                if (!Regex.IsMatch(value, string.Format("\\b{0}\\b", priorityPay)) && !value.Contains("*")) continue;
                // По шифру документа
                value = Convert.ToString(item[0]);
                if (!Regex.IsMatch(value, string.Format("\\b{0}\\b", kinddoc)) && !value.Contains("*")) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Платеж в бюджетную систему
        /// </summary>
        /// <param name="straccountR">Счет получателя</param>
        /// <param name="ubs"></param>
        /// <returns>true - платеж в бюджетную систему РФ</returns>
        private static bool IsBudgetPayment(string straccountR, IUbsWss ubs) {
            //Операционный день                     Бал. счета опред. плат. в бюджет. сист. РФ
            object[] item1 = (object[])ubs.UbsWssParam("Установка", "Операционный день", "Бал. счета опред. плат. в бюджет. сист. РФ");

            for (int i = 0; i < item1.Length; i++) {
                string item = Convert.ToString(item1[i]).Trim();
                if (straccountR.StartsWith(item, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Определить необходимость прохождения контроля ПОД/ФТ для документа раздела А
        /// </summary>
        /// <param name="document">Документ раздела А</param>
        /// <param name="message">Причина попадания на дополнительный контроль ПОД/ФТ</param>
        /// <returns>true - Документ следует поставить на контроля ПОД/ФТ</returns>
        public bool CheckPodFTControl(UbsODPayDoc document, out string message) {
            message = null;

            string innP = null, nameP = null, innR, nameR;
            List<string> errors = new List<string>();

            UbsOgo207pCorrespondent correspondent = new UbsOgo207pCorrespondent(connection, ubs);
            
            this.GetDocumentInnPodFT(document, true, out innP, out nameP);
            this.GetDocumentInnPodFT(document, false, out innR, out nameR);

            if (!string.IsNullOrEmpty(innP) && correspondent.ReadF(innP) != 0 && correspondent.IsTrusty &&
               !string.IsNullOrEmpty(innR) && (correspondent.ReadF(innR) == 0 || correspondent.IsTrusty)) {
                return true;
            }

            if (!string.IsNullOrEmpty(innP) && correspondent.ReadF(innP) !=0 && !correspondent.IsTrusty)
                errors.Add(string.Format("Плательщик <ИНН {0}> имеет действующий признак ненадежного корреспондента", innP));
            if (!string.IsNullOrEmpty(innR) && correspondent.ReadF(innR) != 0 && !correspondent.IsTrusty)
                errors.Add(string.Format("Получатель <ИНН {0}> имеет действующий признак ненадежного корреспондента", innR));

            if (errors.Count > 0) {
                message = string.Join("//", errors.ToArray());
                return false;
            }


            // Если требуется контроль, проверяем его нужен ли он
            connection.CmdText =
                "select count(1) from COM_SETUP_SECTION n" +
                    " inner join COM_SETUP_SETTING g on g.ID_SECTION = n.ID_SECTION and n.NAME_SECTION = 'Отчетность государственных органов' and g.NAME_SETTING = '207-П. ДК ПОД/ФТ. Корреспонденция'" +
                    " inner join COM_SETUP_DATA d0 on d0.ID_SETTING = g.ID_SETTING and d0.INDEX_COLUMN = 0" +
                    " inner join COM_SETUP_DATA d1 on d1.ID_SETTING = g.ID_SETTING and d1.INDEX_COLUMN = 1 and d1.INDEX_ROW = d0.INDEX_ROW" +
                    " inner join COM_SETUP_DATA d2 on d2.ID_SETTING = g.ID_SETTING and d2.INDEX_COLUMN = 2 and d2.INDEX_ROW = d0.INDEX_ROW" +
                    " inner join OD_DOC_0 d on d.ID_DOC = " + document.Id +
                        " and isnull(d.STRACCOUNT_P, d.STRACCOUNT_DB) like d0.FIELD_STRING" +
                        " and isnull(d.STRACCOUNT_R, d.STRACCOUNT_CR) like d1.FIELD_STRING" +
                    " inner join COM_RATES_CB r on r.ID_CURRENCY = d.ID_CURRENCY_DB and r.DATE_RATE <= d.DATE_DOC and  r.DATE_NEXT > d.DATE_DOC" +
                        " and (d.OBOROT_DB * r.RATE > d2.FIELD_MONEY or d2.FIELD_MONEY = 0)";

            if (Convert.ToInt32(connection.ExecuteScalar()) > 0)
                errors.Add("Корреспонденция документа удовлетворяет маске в установке ОГО 207-П. ДК ПОД/ФТ. Корреспонденция");

            decimal sum = Convert.ToDecimal(ubs.UbsWssParam("Установка", "Отчетность государственных органов", "207-П. ДК ПОД/ФТ. Пороговая сумма"));
            if (sum > 0) {
                connection.CmdText =
                    "select sum(d.OBOROT_DB * r.RATE) from OD_DOC_0 d" +
                    " inner join OD_DOC_0 d0 on d0.ID_DOC = " + document.Id +
                        " and d0.ID_ACCOUNT_DB = d.ID_ACCOUNT_DB and d.DATE_DOC = d0.DATE_DOC" +
                    " inner join COM_RATES_CB r on r.ID_CURRENCY = d.ID_CURRENCY_DB and r.DATE_RATE <= d.DATE_DOC and r.DATE_NEXT > d.DATE_DOC";
                if (Convert.ToDecimal(connection.ExecuteScalar()) > sum)
                    errors.Add("Документ привысил пороговое значение дебетовых оборотов за день");
            }

            if (errors.Count == 0) return true;
            message = string.Join("//", errors.ToArray());
            return false;
        }

        /// <summary>
        /// Получить ИНН и Наименование корреспондента для ПОД/ФТ
        /// </summary>
        /// <param name="document">Документ разддела А</param>
        /// <param name="byPayer">true - по плательщику, false - по получателю</param>
        /// <param name="inn">ИНН плательщика/получателя</param>
        /// <param name="name">Наименование плательщика/получателя</param>
        public void GetDocumentInnPodFT(UbsODPayDoc document, bool byPayer, out string inn, out string name) {
            inn = byPayer ? document.INN_P : document.INN_R;
            name = byPayer ? document.Name_P : document.Name_R;

            if (string.IsNullOrEmpty(inn)) {
                string straccount = byPayer ? document.Account_P : document.Account_R;
                if (string.IsNullOrEmpty(straccount)) straccount = byPayer ? document.Account_DB : document.Account_CR;

                if (IsCustomerAccount(straccount)) { // Клиентский счет
                    if (document.PayLocate == 0 || document.PayLocate == 1 && byPayer || document.PayLocate == 2 && !byPayer) {
                        UbsODAccount account = new UbsODAccount(this.connection, this.ubs, 0);
                        if (account.ReadF(straccount) > 0 && account.IdClient > 0) {
                            UbsComClient client = new UbsComClient(this.connection, this.ubs);
                            client.Read(account.IdClient);
                            inn = client.INN;
                            if (string.IsNullOrEmpty(name)) name = client.Name;

                        }
                    }
                }
                else {
                    if (document.PayLocate == 0 || document.PayLocate == 1 && byPayer || document.PayLocate == 2 && !byPayer) {
                        inn = this.settingInnBank;
                        if (string.IsNullOrEmpty(name)) name = this.settingNameBank;
                    }
                }
            }
        }
    }
}
