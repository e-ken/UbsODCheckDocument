using System;
using System.Collections.Generic;
using System.Text;

using UbsService;

namespace UbsBusiness {
    public partial class UbsODCheckDocument {

        /// <summary>
        /// Упрощенная информация по счету
        /// </summary>
        public class AccountInfo {
            /// <summary>
            /// Остатки за период
            /// </summary>
            public UbsOD_Saldo[] Saldo { get; set; }
            /// <summary>
            /// Состояние счета
            /// </summary>
            public byte State { get; set; }
            /// <summary>
            /// Активность счета
            /// </summary>
            public byte Activ { get; set; }
            /// <summary>
            /// Тип проверки остатка
            /// </summary>
            public byte VerLimit { get; set; }
            /// <summary>
            /// Сумма проверки остатка
            /// </summary>
            public decimal Limit { get; set; }
            /// <summary>
            /// Дата проверки остатка
            /// </summary>
            public DateTime DateLimit { get; set; }
            /// <summary>
            /// Признак наличия записей о приостановлениях по счету
            /// </summary>
            public bool IsBlockSum { get; set; }
            /// <summary>
            /// Идентификатор валюты
            /// </summary>
            public short IdCurrency { get; set; }
            /// <summary>
            /// Номер счета
            /// </summary>
            public string Straccount { get; set; }
            /// <summary>
            /// Идентификатор счета
            /// </summary>
            public int Id { get; set; }

            /// <summary>
            /// Получить упрощенную информацию по счету
            /// </summary>
            /// <param name="connection">Интерфейс взаимодействия с БД</param>
            /// <param name="straccount">Номер счета</param>
            /// <param name="startDate">Дата начала периода получения остатков</param>
            /// <returns>Упрощенная информация по счету</returns>
            public static AccountInfo Initialize(IUbsDbConnection connection, string straccount, DateTime startDate) {
                connection.ClearParameters();
                connection.CmdText =
                    "select ACTIV, VERLIMIT, LIMIT, DATELIMIT, isnull(IS_BLOK_SUM, 0), ID_CURRENCY, STATE, STRACCOUNT, ID_ACCOUNT" +
                    " from OD_ACCOUNTS0 where STRACCOUNT = '" + straccount + "'";
                return Initialize(connection, connection.ExecuteReadFirstRec(), startDate);
            }
            /// <summary>
            /// Получить упрощенную информацию по счету
            /// </summary>
            /// <param name="connection">Интерфейс взаимодействия с БД</param>
            /// <param name="accountId">Идентификатор счета</param>
            /// <param name="startDate">Дата начала периода получения остатков</param>
            /// <returns>Упрощенная информация по счету</returns>
            public static AccountInfo Initialize(IUbsDbConnection connection, int accountId, DateTime startDate) {
                connection.ClearParameters();
                connection.CmdText =
                    "select ACTIV, VERLIMIT, LIMIT, DATELIMIT, isnull(IS_BLOK_SUM, 0), ID_CURRENCY, STATE, STRACCOUNT, ID_ACCOUNT" +
                    " from OD_ACCOUNTS0 where ID_ACCOUNT = " + accountId;
                return Initialize(connection, connection.ExecuteReadFirstRec(), startDate);
            }

            private static AccountInfo Initialize(IUbsDbConnection connection, object[] record, DateTime startDate) {
                if (record == null) return null;

                AccountInfo accountInfo = new AccountInfo();

                accountInfo.Activ = Convert.ToByte(record[0]);
                accountInfo.VerLimit = Convert.ToByte(record[1]);
                accountInfo.Limit = Convert.ToDecimal(record[2]);
                accountInfo.DateLimit = Convert.ToDateTime(record[3]);
                accountInfo.IsBlockSum = Convert.ToInt32(record[4]) > 0;
                accountInfo.IdCurrency = Convert.ToInt16(record[5]);
                accountInfo.State = Convert.ToByte(record[6]);
                accountInfo.Straccount = Convert.ToString(record[7]);
                accountInfo.Id = Convert.ToInt32(record[8]);

                accountInfo.Saldo = UbsOD_GetSaldo.GetSaldoTrn(connection, 0, false, accountInfo.Id, startDate, dt22220101);

                return accountInfo;
            }
        }
    }
}
