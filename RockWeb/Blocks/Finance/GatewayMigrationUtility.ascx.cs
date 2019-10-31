﻿// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;
using System.Web.UI.WebControls;
using CsvHelper;
using Microsoft.AspNet.SignalR;
using Rock;
using Rock.Data;
using Rock.Financial;
using Rock.Model;
using Rock.MyWell;
using Rock.Web.Cache;
using Rock.Web.UI;

namespace RockWeb.Blocks.Finance
{
    /// <summary>
    /// Financial Gateway Migration Utility
    /// </summary>
    [DisplayName( "Financial Gateway Migration Utility" )]
    [Category( "Finance" )]
    [Description( "Tool to assist in migrating records from NMI to My Well." )]

    #region Block Attributes
    #endregion Block Attributes
    public partial class GatewayMigrationUtility : RockBlock
    {

        #region Attribute Keys

        /// <summary>
        /// Keys to use for Block Attributes
        /// </summary>
        private static class AttributeKey
        {
        }

        #endregion Attribute Keys

        /// <summary>
        /// This holds the reference to the RockMessageHub SignalR Hub context.
        /// </summary>
        private IHubContext _hubContext = GlobalHost.ConnectionManager.GetHubContext<RockMessageHub>();

        /// <summary>
        /// Gets the signal r notification key.
        /// </summary>
        /// <value>
        /// The signal r notification key.
        /// </value>
        public string SignalRNotificationKey
        {
            get
            {
                return string.Format( "GatewayMigrationUtility_BlockId:{0}_SessionId:{1}", this.BlockId, Session.SessionID );
            }
        }

        #region Base Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
            RockPage.AddScriptLink( "~/Scripts/jquery.signalR-2.2.0.min.js", false );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                ShowDetails();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Shows the details.
        /// </summary>
        protected void ShowDetails()
        {
            var migrateSavedAccountsResultSummary = this.GetBlockUserPreference( "MigrateSavedAccountsResultSummary" );
            var migrateSavedAccountsResultDetails = this.GetBlockUserPreference( "MigrateSavedAccountsResultDetails" );

            if ( migrateSavedAccountsResultSummary.IsNotNullOrWhiteSpace() )
            {
                nbMigrateSavedAccounts.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Info;
                nbMigrateSavedAccounts.Text = "Migrate Saved Accounts has already been run.";
                nbMigrateSavedAccounts.Details = migrateSavedAccountsResultDetails.ToString().ConvertCrLfToHtmlBr();
            }

            var migrateScheduledTransactionsResultSummary = this.GetBlockUserPreference( "MigrateScheduledTransactionsResultSummary" );
            var migrateScheduledTransactionsResultDetails = this.GetBlockUserPreference( "MigrateScheduledTransactionsResultDetails" );

            if ( migrateScheduledTransactionsResultSummary.IsNotNullOrWhiteSpace() )
            {
                nbMigrateScheduledTransactions.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Info;
                nbMigrateScheduledTransactions.Text = "Migrate Scheduled Transactions has already been run.";
                nbMigrateScheduledTransactions.Details = migrateScheduledTransactionsResultDetails.ToString().ConvertCrLfToHtmlBr();
            }

            var rockContext = new RockContext();
            var financialGatewayService = new FinancialGatewayService( rockContext );
            var activeGatewayList = financialGatewayService.Queryable().Where( a => a.IsActive == true ).AsNoTracking().ToList();
            var myWellGateways = activeGatewayList.Where( a => a.GetGatewayComponent() is MyWellGateway ).ToList();
            ddlMyWellGateway.Items.Clear();
            foreach ( var myWellGateway in myWellGateways )
            {
                ddlMyWellGateway.Items.Add( new ListItem( myWellGateway.Name, myWellGateway.Id.ToString() ) );
            }

            var nmiGateways = activeGatewayList.Where( a => a.GetGatewayComponent() is Rock.NMI.Gateway ).ToList();
            ddlNMIGateway.Items.Clear();
            foreach ( var nmiGateway in nmiGateways )
            {
                ddlNMIGateway.Items.Add( new ListItem( nmiGateway.Name, nmiGateway.Id.ToString() ) );
            }
        }

        #endregion

        #region Migrate Saved Accounts Related

        /// <summary>
        /// Handles the FileUploaded event of the fuCustomerVaultImportFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="Rock.Web.UI.Controls.FileUploaderEventArgs"/> instance containing the event data.</param>
        protected void fuCustomerVaultImportFile_FileUploaded( object sender, Rock.Web.UI.Controls.FileUploaderEventArgs e )
        {
            btnMigrateSavedAccounts.Enabled = true;
        }

        /// <summary>
        /// 
        /// </summary>
        private class CustomerVaultImportRecord
        {
            /// <summary>
            /// Gets or sets the NMI customer identifier.
            /// </summary>
            /// <value>
            /// The NMI customer identifier.
            /// </value>
            public string NMICustomerId { get; set; }

            /// <summary>
            /// Gets or sets the My Well customer identifier.
            /// </summary>
            /// <value>
            /// The My Well customer identifier.
            /// </value>
            public string MyWellCustomerId { get; set; }
        }

        private abstract class MigrationResult
        {
            
            public string MyWellCustomerId { get; set; }

            public int? PersonId { get; set; }
            public string PersonFullName { get; set; }

            public DateTime MigrationDateTime { get; set; }

            public bool DidMigrateSuccessfully { get; set; }
            public string ResultMessage { get; set; }

            public abstract string GetSummaryDetails();
        }

        private class SavedAccountMigrationResult : MigrationResult
        {
            public string NMICustomerId { get; set; }
            public int FinancialPersonSavedAccountId { get; set; }

            public override string GetSummaryDetails()
            {
                return string.Format( "FinancialPersonSavedAccount.Id: {0} NMI CustomerId: '{1}', My Well CustomerId: '{2}', Result: {3} ",
                FinancialPersonSavedAccountId,
                NMICustomerId,
                MyWellCustomerId,
                this.ResultMessage );
            }
        }

        private class ScheduledTransactionMigrationResult : MigrationResult
        {
            public int ScheduledTransactionId { get; set; }
            public string NMISubscriptionId { get; internal set; }

            public override string GetSummaryDetails()
            {
                return string.Format( "ScheduledTransactionId.Id: {0}, NMI SubscriptionId: '{1}', My Well CustomerId: '{2}', Result: {3} ",
                ScheduledTransactionId,
                NMISubscriptionId,
                MyWellCustomerId,
                this.ResultMessage );
            }
        }


        /// <summary>
        /// Handles the Click event of the btnMigrateSavedAccounts control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnMigrateSavedAccounts_Click( object sender, EventArgs e )
        {
            BinaryFile binaryFile = null;
            var rockContext = new RockContext();
            var binaryFileService = new BinaryFileService( rockContext );
            var binaryFileId = fuCustomerVaultImportFile.BinaryFileId;
            if ( binaryFileId.HasValue )
            {
                binaryFile = binaryFileService.Get( binaryFileId.Value );
            }

            Dictionary<string, string> nmiToMyWellCustomerIdLookup = null;

            var importData = binaryFile.ContentsToString();

            StringReader stringReader = new StringReader( importData );
            CsvReader csvReader = new CsvReader( stringReader );
            csvReader.Configuration.HasHeaderRecord = false;

            nmiToMyWellCustomerIdLookup = csvReader.GetRecords<CustomerVaultImportRecord>().ToDictionary( k => k.NMICustomerId, v => v.MyWellCustomerId );

            var financialGatewayService = new FinancialGatewayService( rockContext );
            var nmiFinancialGatewayID = ddlNMIGateway.SelectedValue.AsInteger();
            var nmiFinancialGateway = financialGatewayService.Get( nmiFinancialGatewayID );
            var nmiGatewayComponent = nmiFinancialGateway.GetGatewayComponent();
            var myWellFinancialGatewayId = ddlMyWellGateway.SelectedValue.AsInteger();
            var myWellFinancialGateway = financialGatewayService.Get( myWellFinancialGatewayId );
            var myWellGatewayComponent = myWellFinancialGateway.GetGatewayComponent() as IHostedGatewayComponent;

            var financialPersonSavedAccountService = new FinancialPersonSavedAccountService( rockContext );
            var nmiPersonSavedAccountList = financialPersonSavedAccountService.Queryable().Where( a => a.FinancialGatewayId == nmiFinancialGatewayID ).ToList();

            var nmiPersonSavedAccountListCount = nmiPersonSavedAccountList.Count();

            List<MigrationResult> migrateSavedAccountResultList = new List<MigrationResult>();

            foreach ( var nmiPersonSavedAccount in nmiPersonSavedAccountList )
            {
                SavedAccountMigrationResult migrateSavedAccountResult = new SavedAccountMigrationResult();

                migrateSavedAccountResult.FinancialPersonSavedAccountId = nmiPersonSavedAccount.Id;
                if ( nmiPersonSavedAccount.PersonAlias != null )
                {
                    migrateSavedAccountResult.PersonId = nmiPersonSavedAccount.PersonAlias.PersonId;
                    migrateSavedAccountResult.PersonFullName = nmiPersonSavedAccount.PersonAlias.Person.FullName;
                }
                else
                {
                    migrateSavedAccountResult.PersonId = null;
                    migrateSavedAccountResult.PersonFullName = "(No person record associated with saved account)";
                }

                // NMI Saves NMI CustomerId to ReferenceNumber and leaves GatewayPersonIdentifier blank, but just in case that changes, look if GatewayPersonIdentifier has a value first
                if ( nmiPersonSavedAccount.GatewayPersonIdentifier.IsNotNullOrWhiteSpace() )
                {
                    migrateSavedAccountResult.NMICustomerId = nmiPersonSavedAccount.GatewayPersonIdentifier;
                }
                else
                {
                    migrateSavedAccountResult.NMICustomerId = nmiPersonSavedAccount.ReferenceNumber;
                }

                migrateSavedAccountResult.MyWellCustomerId = nmiToMyWellCustomerIdLookup.GetValueOrNull( migrateSavedAccountResult.NMICustomerId );
                migrateSavedAccountResult.MigrationDateTime = RockDateTime.Now;

                if ( migrateSavedAccountResult.NMICustomerId.IsNullOrWhiteSpace() )
                {
                    migrateSavedAccountResult.DidMigrateSuccessfully = false;
                    migrateSavedAccountResult.ResultMessage = string.Format(
                        "Saved Account (FinancialPersonSavedAccount.Guid: {0},  GatewayPersonIdentifier: {1}, ReferenceNumber: {2}) doesn't have an NMI Customer ID reference",
                        nmiPersonSavedAccount.Guid,
                        nmiPersonSavedAccount.GatewayPersonIdentifier,
                        nmiPersonSavedAccount.ReferenceNumber );
                }
                else if ( migrateSavedAccountResult.MyWellCustomerId.IsNullOrWhiteSpace() )
                {
                    // NOTE: NMI Customer IDs created after the Vault import file was created won't have a myWellCustomerId
                    migrateSavedAccountResult.DidMigrateSuccessfully = false;
                    migrateSavedAccountResult.ResultMessage = string.Format(
                        "NMI CustomerId {0} not found in Vault Import file",
                        migrateSavedAccountResult.NMICustomerId );
                }
                else
                {
                    nmiPersonSavedAccount.GatewayPersonIdentifier = migrateSavedAccountResult.MyWellCustomerId;
                    nmiPersonSavedAccount.FinancialGatewayId = myWellFinancialGatewayId;
                    migrateSavedAccountResult.DidMigrateSuccessfully = true;
                    migrateSavedAccountResult.ResultMessage = "Success";
                }

                migrateSavedAccountResultList.Add( migrateSavedAccountResult );
            }

            rockContext.SaveChanges();

            string resultSummary;
            if ( migrateSavedAccountResultList.Where( a => a.DidMigrateSuccessfully == false ).Any() )
            {
                resultSummary = string.Format( "Migrated {0} Saved Accounts with {1} accounts that did not migrate.", migrateSavedAccountResultList.Where( a => a.DidMigrateSuccessfully ).Count(), migrateSavedAccountResultList.Where( a => a.DidMigrateSuccessfully == false ).Count() );
            }
            else
            {
                resultSummary = string.Format( "Migrated {0} Saved Accounts", nmiPersonSavedAccountList.Count(), migrateSavedAccountResultList.Where( a => a.DidMigrateSuccessfully == false ) );
            }

            if ( !nmiPersonSavedAccountList.Any() )
            {
                nbMigrateSavedAccounts.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Warning;
                nbMigrateSavedAccounts.Text = "No NMI Saved Accounts Found";
            }
            else
            {
                nbMigrateSavedAccounts.Title = "Complete";
                nbMigrateSavedAccounts.NotificationBoxType = Rock.Web.UI.Controls.NotificationBoxType.Info;
                nbMigrateSavedAccounts.Text = resultSummary;
            }

            var migrationDetails = migrateSavedAccountResultList.Select( a => a.GetSummaryDetails() ).ToList().AsDelimited( Environment.NewLine );

            nbMigrateSavedAccounts.Visible = true;
            nbMigrateSavedAccounts.Details = string.Format( "<pre>{0}</pre>", migrationDetails );
            this.SetBlockUserPreference( "MigrateSavedAccountsResultSummary", nbMigrateSavedAccounts.Text );
            this.SetBlockUserPreference( "MigrateSavedAccountsResultDetails", migrationDetails );

            try
            {
                string logFile = this.Context.Server.MapPath( string.Format( "~/App_Data/Logs/GatewayMigrationUtility_MigrateSavedAccounts_{0}.json", RockDateTime.Now.ToString( "yyyyMMddTHHmmss" ) ) );
                File.WriteAllText( logFile, migrateSavedAccountResultList.ToJson( Newtonsoft.Json.Formatting.Indented ) );
            }
            catch
            {
                //
            }
        }

        #endregion Migrate Saved Accounts Related

        #region Migrate Scheduled Transactions

        /// <summary>
        /// 
        /// </summary>
        private class SubscriptionCustomerImportRecord
        {
            /// <summary>
            /// Gets or sets the NMI subscription identifier.
            /// </summary>
            /// <value>
            /// The NMI subscription identifier.
            /// </value>
            public string NMISubscriptionId { get; set; }

            /// <summary>
            /// Gets or sets the My Well customer identifier.
            /// </summary>
            /// <value>
            /// The My Well customer identifier.
            /// </value>
            public string MyWellCustomerId { get; set; }
        }

        /// <summary>
        /// Handles the FileUploaded event of the fuScheduleImportFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="Rock.Web.UI.Controls.FileUploaderEventArgs"/> instance containing the event data.</param>
        protected void fuScheduleImportFile_FileUploaded( object sender, Rock.Web.UI.Controls.FileUploaderEventArgs e )
        {
            btnMigrateScheduledTransactions.Enabled = true;
        }

        /// <summary>
        /// Handles the Click event of the btnMigrateScheduledTransactions control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnMigrateScheduledTransactions_Click( object sender, EventArgs e )
        {
            var rockContext = new RockContext();

            var binaryFileService = new BinaryFileService( rockContext );
            var binaryFileId = fuScheduleImportFile.BinaryFileId;

            BinaryFile binaryFile = null;
            if ( binaryFileId.HasValue )
            {
                binaryFile = binaryFileService.Get( binaryFileId.Value );
            }

            Dictionary<string, string> subscriptionImportRecordLookup = null;

            var importData = binaryFile.ContentsToString();

            StringReader stringReader = new StringReader( importData );
            CsvReader csvReader = new CsvReader( stringReader );
            csvReader.Configuration.HasHeaderRecord = false;

            subscriptionImportRecordLookup = csvReader.GetRecords<SubscriptionCustomerImportRecord>().ToDictionary( k => k.NMISubscriptionId, v => v.MyWellCustomerId );

            var financialGatewayService = new FinancialGatewayService( rockContext );
            var nmiFinancialGatewayId = ddlNMIGateway.SelectedValue.AsInteger();
            var nmiFinancialGateway = financialGatewayService.Get( nmiFinancialGatewayId );
            var nmiGatewayComponent = nmiFinancialGateway.GetGatewayComponent();
            var myWellFinancialGatewayId = ddlMyWellGateway.SelectedValue.AsInteger();
            var myWellFinancialGateway = financialGatewayService.Get( myWellFinancialGatewayId );
            var myWellGatewayComponent = myWellFinancialGateway.GetGatewayComponent() as IHostedGatewayComponent;

            var financialScheduledTransactionService = new FinancialScheduledTransactionService( rockContext );

            // Get the ScheduledTransaction with NoTracking. If we need to update it, we'll track it with a different rockContext then save it.
            // Limit to active subscriptions that have a NextPaymentDate (onetime or canceled schedules might not have a NextPaymentDate)
            var scheduledTransactions = financialScheduledTransactionService.Queryable().Where( a => a.FinancialGatewayId == nmiFinancialGatewayId & a.IsActive && a.NextPaymentDate.HasValue ).AsNoTracking().ToList();

            var earliestMyWellStartDate = myWellGatewayComponent.GetEarliestScheduledStartDate( myWellFinancialGateway );
            var oneTimeFrequencyId = DefinedValueCache.GetId( Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_ONE_TIME.AsGuid() );

            string errorMessage;
            
            List<ScheduledTransactionMigrationResult> scheduledTransactionMigrationResults = new List<ScheduledTransactionMigrationResult>();

            var scheduledTransactionCount = scheduledTransactions.Count();
            var scheduledTransactionProgress = 0;

            // Migrating Scheduled Transactions might take a while. Each migrated Scheduled Payment may take a half second or so to create on the MyWell Gateway.
            var importTask = new Task( () =>
            {
                // wait a little so the browser can render and start listening to events
                Task.Delay( 2000 ).Wait();
                _hubContext.Clients.All.setButtonVisibilty( this.SignalRNotificationKey, false );

                foreach ( var scheduledTransaction in scheduledTransactions )
                {
                    UpdateProgressMessage( string.Format( "Migrating Scheduled Transactions: {0} of {1}", scheduledTransactionProgress, scheduledTransactionCount ), " " );

                    scheduledTransactionProgress++;

                    ScheduledTransactionMigrationResult scheduledTransactionMigrationResult = new ScheduledTransactionMigrationResult();
                    scheduledTransactionMigrationResult.MigrationDateTime = RockDateTime.Now;
                    scheduledTransactionMigrationResults.Add( scheduledTransactionMigrationResult );

                    scheduledTransactionMigrationResult.NMISubscriptionId = scheduledTransaction.GatewayScheduleId;
                    if ( scheduledTransactionMigrationResult.NMISubscriptionId.IsNotNullOrWhiteSpace() )
                    {
                        scheduledTransactionMigrationResult.MyWellCustomerId = subscriptionImportRecordLookup.GetValueOrNull( scheduledTransactionMigrationResult.NMISubscriptionId );
                    }

                    scheduledTransactionMigrationResult.ScheduledTransactionId = scheduledTransaction.Id;
                    if ( scheduledTransaction.AuthorizedPersonAlias != null )
                    {
                        scheduledTransactionMigrationResult.PersonId = scheduledTransaction.AuthorizedPersonAlias.PersonId;
                        scheduledTransactionMigrationResult.PersonFullName = scheduledTransaction.AuthorizedPersonAlias.Person.FullName;
                    }
                    else
                    {
                        scheduledTransactionMigrationResult.PersonId = null;
                        scheduledTransactionMigrationResult.PersonFullName = "(No person record associated with saved account)";
                    }

                    if ( scheduledTransactionMigrationResult.MyWellCustomerId == null )
                    {
                        scheduledTransactionMigrationResult.ResultMessage = string.Format(
                            "WARNING: No My Well CustomerId found for Financial Scheduled Transaction with Id: {0} which is associated NMI SubscriptionId: '{1}'",
                            scheduledTransaction.Id,
                            scheduledTransactionMigrationResult.NMISubscriptionId
                        );

                        continue;
                    }

                    // My Well requires that NextPaymentDate is in the Future (using UTC). That math is done in the gateway implementation...
                    // if the NextPayment null or earlier than whatever My Well considers the earliest start date, see if we can fix that up by calling GetStatus
                    if ( scheduledTransaction.NextPaymentDate == null || scheduledTransaction.NextPaymentDate < earliestMyWellStartDate )
                    {
                        financialScheduledTransactionService.GetStatus( scheduledTransaction, out errorMessage );
                    }

                    if ( scheduledTransaction.NextPaymentDate == null )
                    {
                        // Shouldn't happen, but just in case
                        scheduledTransactionMigrationResult.ResultMessage = string.Format(
                            "WARNING: Unknown NextPaymentDate for FinancialScheduledTransaction.Id: {0} NMI SubscriptionId: '{1}'" + Environment.NewLine,
                            scheduledTransaction.Id,
                            scheduledTransactionMigrationResult.NMISubscriptionId
                            );

                        continue;
                    }


                    if ( scheduledTransaction.NextPaymentDate < earliestMyWellStartDate )
                    {
                        if ( ( scheduledTransaction.NextPaymentDate > RockDateTime.Today ) && earliestMyWellStartDate.Subtract( scheduledTransaction.NextPaymentDate.Value ).TotalDays <= 2 )
                        {
                            // if the NextPaymentDate is after Today but before the Earliest My Well Start Date, it'll be off by less than 24 hrs, so just reschedule it for the Earliest My Well Start Date
                            scheduledTransaction.NextPaymentDate = earliestMyWellStartDate;
                        }
                        else
                        {
                            // if the NextPaymentDate is still too early AFTER getting the most recent status, then we can't safely figure it out, so report it
                            scheduledTransactionMigrationResult.ResultMessage = string.Format(
        "WARNING: NextPaymentDate of {0} for FinancialScheduledTransaction.Id: {1} and NMI SubscriptionId: '{2}' must have a NextPaymentDate of at least {3}." + Environment.NewLine,
        scheduledTransaction.NextPaymentDate,
        scheduledTransaction.Id,
        scheduledTransactionMigrationResult.NMISubscriptionId,
        earliestMyWellStartDate
        );
                        }
                    }

                    // create a subscription in the My Well System, then cancel the one on the NMI system
                    PaymentSchedule paymentSchedule = new PaymentSchedule
                    {
                        TransactionFrequencyValue = DefinedValueCache.Get( scheduledTransaction.TransactionFrequencyValueId ),
                        StartDate = scheduledTransaction.NextPaymentDate.Value,
                        PersonId = scheduledTransaction.AuthorizedPersonAlias.PersonId
                    };

                    ReferencePaymentInfo referencePaymentInfo = new ReferencePaymentInfo
                    {
                        GatewayPersonIdentifier = scheduledTransactionMigrationResult.MyWellCustomerId,
                        Description = string.Format( "Migrated from NMI SubscriptionID:{0}", scheduledTransactionMigrationResult.NMISubscriptionId )
                    };

                    var myWellGateway = ( myWellGatewayComponent as MyWellGateway );
                    string alreadyMigratedMyWellSubscriptionId = null;

                    if ( myWellGateway != null )
                    {
                        var customerMyWellSubscriptions = myWellGateway.SearchCustomerSubscriptions( myWellFinancialGateway, scheduledTransactionMigrationResult.MyWellCustomerId );
                        alreadyMigratedMyWellSubscriptionId = customerMyWellSubscriptions.Data.Where( a => a.Description.Contains( referencePaymentInfo.Description ) ).Select( a => a.Customer.Id ).FirstOrDefault();
                    }

                    if ( string.IsNullOrEmpty( alreadyMigratedMyWellSubscriptionId ) )
                    {
                        // hasn't already been migrated, so go ahead and migrate it
                        var tempFinancialScheduledTransaction = myWellGatewayComponent.AddScheduledPayment( myWellFinancialGateway, paymentSchedule, referencePaymentInfo, out errorMessage );
                        if ( tempFinancialScheduledTransaction != null )
                        {
                            ////////////#### DISABLE this when debugging #####
                            nmiGatewayComponent.CancelScheduledPayment( scheduledTransaction, out errorMessage );

                            // update the scheduled transaction to point to the MyWell scheduled transaction
                            using ( var updateRockContext = new RockContext() )
                            {
                                // Attach the person to the updateRockContext so that it'll be tracked/saved using updateRockContext 
                                updateRockContext.FinancialScheduledTransactions.Attach( scheduledTransaction );
                                scheduledTransaction.TransactionCode = tempFinancialScheduledTransaction.TransactionCode;
                                scheduledTransaction.GatewayScheduleId = tempFinancialScheduledTransaction.GatewayScheduleId;
                                scheduledTransaction.FinancialGatewayId = tempFinancialScheduledTransaction.FinancialGatewayId;
                                updateRockContext.SaveChanges();
                            }

                            scheduledTransactionMigrationResult.DidMigrateSuccessfully = true;

                            scheduledTransactionMigrationResult.ResultMessage = string.Format(
                                "SUCCESS: Scheduled Transaction migration succeeded. (FinancialScheduledTransaction.Id: {0}, NMI SubscriptionId: '{1}', My Well CustomerId: {2}, My Well SubscriptionId: {3})" + Environment.NewLine,
                                scheduledTransaction.Id,
                                scheduledTransactionMigrationResult.NMISubscriptionId,
                                scheduledTransactionMigrationResult.MyWellCustomerId,
                                scheduledTransaction.GatewayScheduleId
                                );
                        }
                        else
                        {
                            scheduledTransactionMigrationResult.ResultMessage = string.Format(
                                "ERROR: Scheduled Transaction migration failed. ErrorMessage: {0}, FinancialScheduledTransaction.Id: {1}, NMI SubscriptionId: '{2}', My Well CustomerId: {3}" + Environment.NewLine,
                                errorMessage,
                                scheduledTransaction.Id,
                                scheduledTransactionMigrationResult.NMISubscriptionId,
                                scheduledTransactionMigrationResult.MyWellCustomerId
                                );
                        }
                    }
                    else
                    {
                        scheduledTransactionMigrationResult.ResultMessage = string.Format(
                            "INFO: Scheduled Transaction already migrated to My Well. FinancialScheduledTransaction.Id: {0}, NMI SubscriptionId: '{1}', My Well SubscriptionId: '{2}', My Well CustomerId: {3}" + Environment.NewLine,
                            scheduledTransaction.Id,
                            scheduledTransactionMigrationResult.NMISubscriptionId,
                            alreadyMigratedMyWellSubscriptionId,
                            scheduledTransactionMigrationResult.MyWellCustomerId
                            );
                    }
                }
            } );

            string importResult = string.Empty;

            importTask.ContinueWith( ( c ) =>
             {
                 var migrationDetails = scheduledTransactionMigrationResults.Select( a => a.GetSummaryDetails() ).ToList().AsDelimited( Environment.NewLine );

                 if ( c.Exception != null )
                 {
                     ExceptionLogService.LogException( c.Exception );
                     migrationDetails += string.Format( "EXCEPTION: {0}", c.Exception.Flatten().Message );
                     importResult = "EXCEPTION";
                     UpdateProgressMessage( importResult, migrationDetails );
                 }
                 else
                 {
                     importResult = "Migrate Scheduled Transactions Completed Successfully";
                     UpdateProgressMessage( importResult, migrationDetails );
                 }

                 this.SetBlockUserPreference( "MigrateScheduledTransactionsResultSummary", importResult );
                 this.SetBlockUserPreference( "MigrateScheduledTransactionsResultDetails", migrationDetails );

                 try
                 {
                     string logFile = this.Context.Server.MapPath( string.Format( "~/App_Data/Logs/GatewayMigrationUtility_MigrateScheduledTransactions_{0}.json", RockDateTime.Now.ToString( "yyyyMMddTHHmmss" ) ) );
                     File.WriteAllText( logFile, scheduledTransactionMigrationResults.ToJson( Newtonsoft.Json.Formatting.Indented ) );
                 }
                 catch
                 {
                     //
                 }
             } );

            importTask.Start();

            nbMigrateScheduledTransactions.Visible = false;
        }

        /// <summary>
        /// Updates the progress message.
        /// </summary>
        /// <param name="progressMessage">The progress message.</param>
        public void UpdateProgressMessage( string progressMessage, string results )
        {
            _hubContext.Clients.All.showProgress( this.SignalRNotificationKey, progressMessage, results );
        }
    }

    #endregion Migrate Scheduled Transactions
}