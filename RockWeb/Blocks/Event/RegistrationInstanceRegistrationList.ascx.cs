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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Blocks.Event
{
    /// <summary>
    /// A Block that displays the list of Registrations related to a Registration Instance.
    /// </summary>
    [DisplayName( "Registration Instance - Registration List" )]
    [Category( "Event" )]
    [Description( "Displays the list of Registrations related to a Registration Instance." )]

    #region Block Attributes

    [LinkedPage(
        "Registration Page",
        "The page for editing registration and registrant information",
        Key = AttributeKey.RegistrationPage,
        IsRequired = false,
        Order = 1 )]
    [BooleanField(
        "Display Discount Codes",
        "Display the discount code used with a payment",
        Key = AttributeKey.DisplayDiscountCodes,
        DefaultBooleanValue = false,
        Order = 2 )]

    #endregion Block Attributes

    public partial class RegistrationInstanceRegistrationList : Rock.Web.UI.RockBlock
    {
        #region Attribute Keys

        /// <summary>
        /// Keys to use for Block Attributes
        /// </summary>
        private static class AttributeKey
        {
            /// <summary>
            /// The linked page used to display registration details.
            /// </summary>
            public const string RegistrationPage = "RegistrationPage";

            /// <summary>
            /// Should discount codes be displayed in the list?
            /// </summary>
            public const string DisplayDiscountCodes = "DisplayDiscountCodes";
        }

        #endregion Attribute Keys

        #region Page Parameter Keys

        /// <summary>
        /// Keys to use for Page Parameters
        /// </summary>
        private static class PageParameterKey
        {
            /// <summary>
            /// The Registration Instance identifier
            /// </summary>
            public const string RegistrationInstanceId = "RegistrationInstanceId";

            /// <summary>
            /// The Registration Template identifier.
            /// </summary>
            //public const string RegistrationTemplateId = "RegistrationTemplateId";
        }

        #endregion Page Parameter Keys

        #region Fields

        private RegistrationInstanceRegistrationListViewModel _ViewModel;

        private List<FinancialTransactionDetail> registrationPayments;
        
        private bool _instanceHasCost = false;

        /// <summary>
        /// Gets or sets the available registration attributes where IsGridColumn = true
        /// </summary>
        /// <value>
        /// The available attributes.
        /// </value>
        public List<AttributeCache> AvailableRegistrationAttributesForGrid { get; set; }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the registration template identifier.
        /// </summary>
        /// <value>
        /// The registration template identifier.
        /// </value>
        protected int? RegistrationTemplateId { get; set; }

        #endregion

        #region Base Control Methods

        /// <summary>
        /// Restores the view-state information from a previous user control request that was saved by the <see cref="M:System.Web.UI.UserControl.SaveViewState" /> method.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Object" /> that represents the user control state to be restored.</param>
        protected override void LoadViewState( object savedState )
        {
            base.LoadViewState( savedState );

            AvailableRegistrationAttributesForGrid = ViewState["AvailableRegistrationAttributesForGrid"] as List<AttributeCache>;

            RegistrationTemplateId = ViewState["RegistrationTemplateId"] as int? ?? 0;

            // don't set the values if this is a postback from a grid 'ClearFilter'
            bool setValues = this.Request.Params["__EVENTTARGET"] == null || !this.Request.Params["__EVENTTARGET"].EndsWith( "_lbClearFilter" );
            SetUserPreferencePrefix( RegistrationTemplateId.Value );
            AddDynamicControls( setValues );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            _ViewModel = new RegistrationInstanceRegistrationListViewModel();

            _ViewModel.ContextManager = new BlockContextManager( string.Format( "SharedItem:Page:{0}:Item:", this.RockPage.PageId ), System.Web.HttpContext.Current.Items );

            fRegistrations.ApplyFilterClick += fRegistrations_ApplyFilterClick;
            gRegistrations.DataKeyNames = new string[] { "Id" };
            gRegistrations.Actions.ShowAdd = true;
            gRegistrations.Actions.AddClick += gRegistrations_AddClick;
            gRegistrations.RowDataBound += gRegistrations_RowDataBound;
            gRegistrations.GridRebind += gRegistrations_GridRebind;
            gRegistrations.ShowConfirmDeleteDialog = false;

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            InitializeActiveRegistrationInstance();

            if ( !Page.IsPostBack )
            {
                ShowDetail();
            }            
        }

        /// <summary>
        /// Saves any user control view-state changes that have occurred since the last page postback.
        /// </summary>
        /// <returns>
        /// Returns the user control's current view state. If there is no view state associated with the control, it returns null.
        /// </returns>
        protected override object SaveViewState()
        {
			ViewState["RegistrationTemplateId"] = RegistrationTemplateId;
            ViewState["AvailableRegistrationAttributesForGrid"] = AvailableRegistrationAttributesForGrid;

            return base.SaveViewState();
        }

        /// <summary>
        /// Gets the bread crumbs.
        /// </summary>
        /// <param name="pageReference">The page reference.</param>
        /// <returns></returns>
        public override List<BreadCrumb> GetBreadCrumbs( PageReference pageReference )
        {
            var breadCrumbs = new List<BreadCrumb>();

            /*
             * This method executes prior to the Init and Load events.
             * Therefore, we need to construct a temporary viewmodel to retrieve the necessary data.
            */
            var viewModel = new RegistrationInstanceRegistrationListViewModel();

            viewModel.LoadRegistrationInstance( this.PageParameter( pageReference, PageParameterKey.RegistrationInstanceId ).AsIntegerOrNull() );

            if ( viewModel.RegistrationInstance != null )
            {
                breadCrumbs.Add( new BreadCrumb( viewModel.RegistrationInstance.ToString(), pageReference ) );
                return breadCrumbs;
            }

            breadCrumbs.Add( new BreadCrumb( "New Registration Instance", pageReference ) );

            return breadCrumbs;
        }

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
        }

        #endregion

        #region Events

        #region Main Form Events

        
        private void AddDynamicControls( bool setValues )
        {
            RegistrationsTabAddDynamicControls( setValues );
        }

        #endregion

        #region Registration Tab Events

        /// <summary>
        /// Handles the ApplyFilterClick event of the fRegistrations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void fRegistrations_ApplyFilterClick( object sender, EventArgs e )
        {
            fRegistrations.SaveUserPreference( "Registrations Date Range", "Registration Date Range", sdrpRegistrationDateRange.DelimitedValues );
            fRegistrations.SaveUserPreference( "Payment Status", ddlRegistrationPaymentStatus.SelectedValue );
            fRegistrations.SaveUserPreference( "RegisteredBy First Name", tbRegistrationRegisteredByFirstName.Text );
            fRegistrations.SaveUserPreference( "RegisteredBy Last Name", tbRegistrationRegisteredByLastName.Text );
            fRegistrations.SaveUserPreference( "Registrant First Name", tbRegistrationRegistrantFirstName.Text );
            fRegistrations.SaveUserPreference( "Registrant Last Name", tbRegistrationRegistrantLastName.Text );

            // Store the selected date range in the page context so it can be used to synchronise the data displayed by other blocks, such as the RegistrationInstanceGroupPlacement block.
            RockPage.SaveSharedItem( "RegistrationDateRange", DateRange.FromDelimitedValues( sdrpRegistrationDateRange.DelimitedValues ) );

            BindRegistrationsGrid();
        }

        /// <summary>
        /// Handles the ClearFilterClick event of the fRegistrations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void fRegistrations_ClearFilterClick( object sender, EventArgs e )
        {
            fRegistrations.SaveUserPreference( "Registrations Date Range", "Registration Date Range", string.Empty );
            fRegistrations.SaveUserPreference( "Payment Status", string.Empty );
            fRegistrations.SaveUserPreference( "RegisteredBy First Name", string.Empty );
            fRegistrations.SaveUserPreference( "RegisteredBy Last Name", string.Empty );
            fRegistrations.SaveUserPreference( "Registrant First Name", string.Empty );
            fRegistrations.SaveUserPreference( "Registrant Last Name", string.Empty );

            //fRegistrants.DeleteUserPreferences();
            BindRegistrationsFilter();
        }

        /// <summary>
        /// Fs the registrations_ display filter value.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        protected void fRegistrations_DisplayFilterValue( object sender, GridFilter.DisplayFilterValueArgs e )
        {
            switch ( e.Key )
            {
                case "Registrations Date Range":
                    e.Value = SlidingDateRangePicker.FormatDelimitedValues( e.Value );
                    break;

                case "Payment Status":
                case "RegisteredBy First Name":
                case "RegisteredBy Last Name":
                case "Registrant First Name":
                case "Registrant Last Name":
                    break;
                    
                default:
                   e.Value = string.Empty;
                    break;
            }
        }

        /// <summary>
        /// Handles the GridRebind event of the gRegistrations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void gRegistrations_GridRebind( object sender, EventArgs e )
        {
            if ( _RegistrationInstance == null )
            {
                return;
            }

            gRegistrations.ExportTitleName = _RegistrationInstance.Name + " - Registrations";
            gRegistrations.ExportFilename = gRegistrations.ExportFilename ?? _RegistrationInstance.Name + "Registrations";
            BindRegistrationsGrid();
        }

        /// <summary>
        /// Handles the RowDataBound event of the gRegistrations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridViewRowEventArgs"/> instance containing the event data.</param>
        protected void gRegistrations_RowDataBound( object sender, GridViewRowEventArgs e )
        {
            var registration = e.Row.DataItem as Registration;
            if ( registration != null )
            {
                // Set the processor value
                var lRegisteredBy = e.Row.FindControl( "lRegisteredBy" ) as Literal;
                if ( lRegisteredBy != null )
                {
                    if ( registration.PersonAlias != null && registration.PersonAlias.Person != null )
                    {
                        lRegisteredBy.Text = registration.PersonAlias.Person.FullNameReversed;
                    }
                    else
                    {
                        lRegisteredBy.Text = string.Format( "{0}, {1}", registration.LastName, registration.FirstName );
                    }
                }

                string registrantNames = string.Empty;
                if ( registration.Registrants != null && registration.Registrants.Any() )
                {
                    var registrants = registration.Registrants
                        .Where( r =>
                            r.PersonAlias != null &&
                            r.PersonAlias.Person != null )
                        .OrderBy( r => r.PersonAlias.Person.NickName )
                        .ThenBy( r => r.PersonAlias.Person.LastName )
                        .ToList();

                    registrantNames = registrants
                        .Select( r => r.OnWaitList ? r.PersonAlias.Person.NickName + " " + r.PersonAlias.Person.LastName + " <span class='label label-warning'>WL</span>" : r.PersonAlias.Person.NickName + " " + r.PersonAlias.Person.LastName )
                        .ToList()
                        .AsDelimited( "<br/>" );
                }

                // Set the Registrants
                var lRegistrants = e.Row.FindControl( "lRegistrants" ) as Literal;
                if ( lRegistrants != null )
                {
                    lRegistrants.Text = registrantNames;
                }

                var payments = registrationPayments.Where( p => p.EntityId == registration.Id );
                bool hasPayments = payments.Any();
                decimal totalPaid = hasPayments ? payments.Select( p => p.Amount ).DefaultIfEmpty().Sum() : 0.0m;

                // Set the Cost
                decimal discountedCost = registration.DiscountedCost;
                var lRegistrationCost = e.Row.FindControl( "lRegistrationCost" ) as Literal;
                if ( lRegistrationCost != null )
                {
                    lRegistrationCost.Visible = _instanceHasCost || discountedCost > 0.0M;
                    lRegistrationCost.Text = string.Format( "<span class='label label-info'>{0}</span>", discountedCost.FormatAsCurrency() );
                }

                var discountCode = registration.DiscountCode;
                var lDiscount = e.Row.FindControl( "lDiscount" ) as Literal;
                if ( lDiscount != null )
                {
                    lDiscount.Visible = _instanceHasCost && !string.IsNullOrEmpty( discountCode );
                    lDiscount.Text = string.Format( "<span class='label label-default'>{0}</span>", discountCode );
                }

                var lBalance = e.Row.FindControl( "lBalance" ) as Literal;
                if ( lBalance != null )
                {
                    decimal balanceDue = registration.DiscountedCost - totalPaid;
                    lBalance.Visible = _instanceHasCost || discountedCost > 0.0M;
                    string balanceCssClass;
                    if ( balanceDue > 0.0m )
                    {
                        balanceCssClass = "label-danger";
                    }
                    else if ( balanceDue < 0.0m )
                    {
                        balanceCssClass = "label-warning";
                    }
                    else
                    {
                        balanceCssClass = "label-success";
                    }

                    lBalance.Text = string.Format(
    @"<span class='label {0}'>{1}</span>
    <input type='hidden' class='js-has-payments' value='{2}' />", balanceCssClass, balanceDue.FormatAsCurrency(), hasPayments.ToTrueFalse() );
                }
            }
        }

        /// <summary>
        /// Handles the AddClick event of the gRegistrations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void gRegistrations_AddClick( object sender, EventArgs e )
        {
            NavigateToLinkedPage( AttributeKey.RegistrationPage, "RegistrationId", 0, PageParameterKey.RegistrationInstanceId, hfRegistrationInstanceId.ValueAsInt() );
        }

        /// <summary>
        /// Handles the Delete event of the gRegistrations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gRegistrations_Delete( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var registrationService = new RegistrationService( rockContext );
                var registration = registrationService.Get( e.RowKeyId );
                if ( registration != null )
                {
                    int registrationInstanceId = registration.RegistrationInstanceId;

                    if ( !UserCanEdit &&
                        !registration.IsAuthorized( "Register", CurrentPerson ) &&
                        !registration.IsAuthorized( Authorization.EDIT, this.CurrentPerson ) &&
                        !registration.IsAuthorized( Authorization.ADMINISTRATE, this.CurrentPerson ) )
                    {
                        mdDeleteWarning.Show( "You are not authorized to delete this registration.", ModalAlertType.Information );
                        return;
                    }

                    string errorMessage;
                    if ( !registrationService.CanDelete( registration, out errorMessage ) )
                    {
                        mdRegistrationsGridWarning.Show( errorMessage, ModalAlertType.Information );
                        return;
                    }

                    var changes = new History.HistoryChangeList();
                    changes.AddChange( History.HistoryVerb.Delete, History.HistoryChangeType.Record, "Registration" );

                    rockContext.WrapTransaction( () =>
                    {
                        HistoryService.SaveChanges(
                            rockContext,
                            typeof( Registration ),
                            Rock.SystemGuid.Category.HISTORY_EVENT_REGISTRATION.AsGuid(),
                            registration.Id,
                            changes );

                        registrationService.Delete( registration );
                        rockContext.SaveChanges();
                    } );

                    SetHasPayments( registrationInstanceId, rockContext );
                }
            }

            BindRegistrationsGrid();
        }

        /// <summary>
        /// Handles the RowSelected event of the gRegistrations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gRegistrations_RowSelected( object sender, RowEventArgs e )
        {
            NavigateToLinkedPage( AttributeKey.RegistrationPage, "RegistrationId", e.RowKeyId );
        }

        #endregion
        
        #endregion

        #region Main Form Methods

        private RegistrationInstance _RegistrationInstance = null;

        /// <summary>
        /// Load the active Registration Instance for the current page context.
        /// </summary>
        private void InitializeActiveRegistrationInstance()
        {
            _RegistrationInstance = null;
            
            int? registrationInstanceId = this.PageParameter( PageParameterKey.RegistrationInstanceId ).AsInteger();

            if ( registrationInstanceId != 0 )
            {
                _RegistrationInstance = GetRegistrationInstance( registrationInstanceId.Value );
            }

            hfRegistrationInstanceId.Value = registrationInstanceId.ToString();
        }

        /// <summary>
        /// Gets the registration instance.
        /// </summary>
        /// <param name="registrationInstanceId">The registration instance identifier.</param>
        /// <param name="rockContext">The rock context.</param>
        /// <returns></returns>
        private RegistrationInstance GetRegistrationInstance( int registrationInstanceId, RockContext rockContext = null )
        {
            string key = string.Format( "RegistrationInstance:{0}", registrationInstanceId );
            RegistrationInstance registrationInstance = RockPage.GetSharedItem( key ) as RegistrationInstance;
            if ( registrationInstance == null )
            {
                rockContext = rockContext ?? new RockContext();
                registrationInstance = new RegistrationInstanceService( rockContext )
                    .Queryable( "RegistrationTemplate,Account,RegistrationTemplate.Forms.Fields" )
                    .AsNoTracking()
                    .FirstOrDefault( i => i.Id == registrationInstanceId );
                RockPage.SaveSharedItem( key, registrationInstance );
            }

            return registrationInstance;
        }

        /// <summary>
        /// Shows the detail.
        /// </summary>
        private void ShowDetail()
        {
            int? registrationInstanceId = PageParameter( PageParameterKey.RegistrationInstanceId ).AsIntegerOrNull();
            int? parentTemplateId = PageParameter( "RegistrationTemplateId" ).AsIntegerOrNull();

            if ( !registrationInstanceId.HasValue )
            {
                pnlDetails.Visible = false;
                return;
            }

            using ( var rockContext = new RockContext() )
            {
                RegistrationInstance registrationInstance = null;
                if ( registrationInstanceId.HasValue )
                {
                    registrationInstance = GetRegistrationInstance( registrationInstanceId.Value, rockContext );
                }

                if ( registrationInstance == null )
                {
                    registrationInstance = new RegistrationInstance();
                    registrationInstance.Id = 0;
                    registrationInstance.IsActive = true;
                    registrationInstance.RegistrationTemplateId = parentTemplateId ?? 0;

                    Guid? accountGuid = GetAttributeValue( "DefaultAccount" ).AsGuidOrNull();
                    if ( accountGuid.HasValue )
                    {
                        var account = new FinancialAccountService( rockContext ).Get( accountGuid.Value );
                        registrationInstance.AccountId = account != null ? account.Id : 0;
                    }
                }

                if ( registrationInstance.RegistrationTemplate == null && registrationInstance.RegistrationTemplateId > 0 )
                {
                    registrationInstance.RegistrationTemplate = new RegistrationTemplateService( rockContext )
                        .Get( registrationInstance.RegistrationTemplateId );
                }

                AvailableRegistrationAttributesForGrid = new List<AttributeCache>();

                int entityTypeId = new Registration().TypeId;
                foreach ( var attributeCache in new AttributeService( new RockContext() ).GetByEntityTypeQualifier( entityTypeId, "RegistrationTemplateId", registrationInstance.RegistrationTemplateId.ToString(), false )
                    .Where( a => a.IsGridColumn )
                    .OrderBy( a => a.Order )
                    .ThenBy( a => a.Name )
                    .ToAttributeCacheList() )
                {
                    AvailableRegistrationAttributesForGrid.Add( attributeCache );
                }

                //hlType.Visible = registrationInstance.RegistrationTemplate != null;
                //hlType.Text = registrationInstance.RegistrationTemplate != null ? registrationInstance.RegistrationTemplate.Name : string.Empty;

                //lWizardTemplateName.Text = hlType.Text;

                pnlDetails.Visible = true;
                hfRegistrationInstanceId.Value = registrationInstance.Id.ToString();
                hfRegistrationTemplateId.Value = registrationInstance.RegistrationTemplateId.ToString();
                RegistrationTemplateId = registrationInstance.RegistrationTemplateId;
                SetHasPayments( registrationInstance.Id, rockContext );

                // render UI based on Authorized
                bool readOnly = false;

                bool canEdit = UserCanEdit ||
                    registrationInstance.IsAuthorized( Authorization.EDIT, CurrentPerson ) ||
                    registrationInstance.IsAuthorized( Authorization.ADMINISTRATE, CurrentPerson );

                // User must have 'Edit' rights to block, or 'Edit' or 'Administrate' rights to instance
                if ( !canEdit )
                {
                    readOnly = true;
                }

                if ( readOnly )
                {
                    bool allowRegistrationEdit = registrationInstance.IsAuthorized( "Register", CurrentPerson );
                    gRegistrations.Actions.ShowAdd = allowRegistrationEdit;
                    gRegistrations.IsDeleteEnabled = allowRegistrationEdit;
                }

                BindRegistrationsFilter();

                AddDynamicControls( true );

                BindRegistrationsGrid();
            }
        }

        /// <summary>
        /// Sets the user preference prefix.
        /// </summary>
        private void SetUserPreferencePrefix(int registrationTemplateId )
        {
            fRegistrations.UserPreferenceKeyPrefix = string.Format( "{0}-", registrationTemplateId );
        }

        /// <summary>
        /// Sets whether the registration has payments.
        /// </summary>
        /// <param name="registrationInstanceId">The registration instance identifier.</param>
        /// <param name="rockContext">The rock context.</param>
        private void SetHasPayments( int registrationInstanceId, RockContext rockContext )
        {
            var registrationIdQry = new RegistrationService( rockContext )
                .Queryable().AsNoTracking()
                .Where( r =>
                    r.RegistrationInstanceId == registrationInstanceId &&
                    !r.IsTemporary )
                .Select( r => r.Id );

            var registrationEntityType = EntityTypeCache.Get( typeof( Rock.Model.Registration ) );
            hfHasPayments.Value = new FinancialTransactionDetailService( rockContext )
                .Queryable().AsNoTracking()
                .Where( d =>
                    d.EntityTypeId.HasValue &&
                    d.EntityId.HasValue &&
                    d.EntityTypeId.Value == registrationEntityType.Id &&
                    registrationIdQry.Contains( d.EntityId.Value ) )
                .Any().ToString();
        }

        #endregion

        #region Registration Tab

        /// <summary>
        /// Binds the registrations filter.
        /// </summary>
        private void BindRegistrationsFilter()
        {
            sdrpRegistrationDateRange.DelimitedValues = fRegistrations.GetUserPreference( "Registrations Date Range" );
            ddlRegistrationPaymentStatus.SetValue( fRegistrations.GetUserPreference( "Payment Status" ) );
            tbRegistrationRegisteredByFirstName.Text = fRegistrations.GetUserPreference( "RegisteredBy First Name" );
            tbRegistrationRegisteredByLastName.Text = fRegistrations.GetUserPreference( "RegisteredBy Last Name" );
            tbRegistrationRegistrantFirstName.Text = fRegistrations.GetUserPreference( "Registrant First Name" );
            tbRegistrationRegistrantLastName.Text = fRegistrations.GetUserPreference( "Registrant Last Name" );
        }

        /// <summary>
        /// Binds the registrations grid.
        /// </summary>
        private void BindRegistrationsGrid()
        {
            int? instanceId = hfRegistrationInstanceId.Value.AsIntegerOrNull();
            if ( instanceId.HasValue && instanceId > 0)
            {
                using ( var rockContext = new RockContext() )
                {
                    var registrationEntityType = EntityTypeCache.Get( typeof( Rock.Model.Registration ) );

                    var instance = new RegistrationInstanceService( rockContext ).Get( instanceId.Value );
                    if ( instance != null )
                    {
                        decimal cost = instance.RegistrationTemplate.Cost;
                        if ( instance.RegistrationTemplate.SetCostOnInstance ?? false )
                        {
                            cost = instance.Cost ?? 0.0m;
                        }

                        _instanceHasCost = cost > 0.0m;
                    }

                    var qry = new RegistrationService( rockContext )
                        .Queryable( "PersonAlias.Person,Registrants.PersonAlias.Person,Registrants.Fees.RegistrationTemplateFee" )
                        .AsNoTracking()
                        .Where( r =>
                            r.RegistrationInstanceId == instanceId.Value &&
                            !r.IsTemporary );

                    var dateRange = SlidingDateRangePicker.CalculateDateRangeFromDelimitedValues( sdrpRegistrationDateRange.DelimitedValues );

                    if ( dateRange.Start.HasValue )
                    {
                        qry = qry.Where( r =>
                            r.CreatedDateTime.HasValue &&
                            r.CreatedDateTime.Value >= dateRange.Start.Value );
                    }

                    if ( dateRange.End.HasValue )
                    {
                        qry = qry.Where( r =>
                            r.CreatedDateTime.HasValue &&
                            r.CreatedDateTime.Value < dateRange.End.Value );
                    }

                    if ( !string.IsNullOrWhiteSpace( tbRegistrationRegisteredByFirstName.Text ) )
                    {
                        string pfname = tbRegistrationRegisteredByFirstName.Text;
                        qry = qry.Where( r =>
                            r.FirstName.StartsWith( pfname ) ||
                            r.PersonAlias.Person.NickName.StartsWith( pfname ) ||
                            r.PersonAlias.Person.FirstName.StartsWith( pfname ) );
                    }

                    if ( !string.IsNullOrWhiteSpace( tbRegistrationRegisteredByLastName.Text ) )
                    {
                        string plname = tbRegistrationRegisteredByLastName.Text;
                        qry = qry.Where( r =>
                            r.LastName.StartsWith( plname ) ||
                            r.PersonAlias.Person.LastName.StartsWith( plname ) );
                    }

                    if ( !string.IsNullOrWhiteSpace( tbRegistrationRegistrantFirstName.Text ) )
                    {
                        string rfname = tbRegistrationRegistrantFirstName.Text;
                        qry = qry.Where( r =>
                            r.Registrants.Any( p =>
                                p.PersonAlias.Person.NickName.StartsWith( rfname ) ||
                                p.PersonAlias.Person.FirstName.StartsWith( rfname ) ) );
                    }

                    if ( !string.IsNullOrWhiteSpace( tbRegistrationRegistrantLastName.Text ) )
                    {
                        string rlname = tbRegistrationRegistrantLastName.Text;
                        qry = qry.Where( r =>
                            r.Registrants.Any( p =>
                                p.PersonAlias.Person.LastName.StartsWith( rlname ) ) );
                    }

                    // If filtering on payment status, need to do some sub-querying...
                    if ( ddlRegistrationPaymentStatus.SelectedValue != string.Empty && registrationEntityType != null )
                    {
                        // Get all the registrant costs
                        var rCosts = new Dictionary<int, decimal>();
                        qry.ToList()
                            .Select( r => new
                            {
                                RegistrationId = r.Id,
                                DiscountCosts = r.Registrants.Sum( p => (decimal?) p.DiscountedCost( r.DiscountPercentage, r.DiscountAmount) ) ?? 0.0m,
                            } ).ToList()
                            .ForEach( c =>
                                rCosts.AddOrReplace( c.RegistrationId, c.DiscountCosts ) );

                        var rPayments = new Dictionary<int, decimal>();
                        new FinancialTransactionDetailService( rockContext )
                            .Queryable().AsNoTracking()
                            .Where( d =>
                                d.EntityTypeId.HasValue &&
                                d.EntityId.HasValue &&
                                d.EntityTypeId.Value == registrationEntityType.Id &&
                                rCosts.Keys.Contains( d.EntityId.Value ) )
                            .Select( d => new
                            {
                                RegistrationId = d.EntityId.Value,
                                Payment = d.Amount
                            } )
                            .ToList()
                            .GroupBy( d => d.RegistrationId )
                            .Select( d => new
                            {
                                RegistrationId = d.Key,
                                Payments = d.Sum( p => p.Payment )
                            } )
                            .ToList()
                            .ForEach( p => rPayments.AddOrReplace( p.RegistrationId, p.Payments ) );

                        var rPmtSummary = rCosts
                            .Join(
                                rPayments,
                                c => c.Key,
                                p => p.Key, 
                                ( c, p ) => new
                                {
                                    RegistrationId = c.Key,
                                    Costs = c.Value,
                                    Payments = p.Value
                                } )
                            .ToList();

                        var ids = new List<int>();

                        if ( ddlRegistrationPaymentStatus.SelectedValue == "Paid in Full" )
                        {
                            ids = rPmtSummary
                                .Where( r => r.Costs <= r.Payments )
                                .Select( r => r.RegistrationId )
                                .ToList();
                        }
                        else
                        {
                            ids = rPmtSummary
                                .Where( r => r.Costs > r.Payments )
                                .Select( r => r.RegistrationId )
                                .ToList();
                        }

                        qry = qry.Where( r => ids.Contains( r.Id ) );
                    }

                    SortProperty sortProperty = gRegistrations.SortProperty;
                    if ( sortProperty != null )
                    {
                        // If sorting by Total Cost or Balance Due, the database query needs to be run first without ordering,
                        // and then ordering needs to be done in memory since TotalCost and BalanceDue are not database fields.
                        if ( sortProperty.Property == "TotalCost" )
                        {
                            if ( sortProperty.Direction == SortDirection.Ascending )
                            {
                                gRegistrations.SetLinqDataSource( qry.ToList().OrderBy( r => r.TotalCost ).AsQueryable() );
                            }
                            else
                            {
                                gRegistrations.SetLinqDataSource( qry.ToList().OrderByDescending( r => r.TotalCost ).AsQueryable() );
                            }
                        }
                        else if ( sortProperty.Property == "BalanceDue" )
                        {
                            if ( sortProperty.Direction == SortDirection.Ascending )
                            {
                                gRegistrations.SetLinqDataSource( qry.ToList().OrderBy( r => r.BalanceDue ).AsQueryable() );
                            }
                            else
                            {
                                gRegistrations.SetLinqDataSource( qry.ToList().OrderByDescending( r => r.BalanceDue ).AsQueryable() );
                            }
                        }
                        else if ( sortProperty.Property == "RegisteredBy" )
                        {
                            // Sort by the Person name if we have it, otherwise the provided first and last name.
                            Func<Registration, string> sortBy = ( r ) =>
                            {
                                return r.PersonAlias != null && r.PersonAlias.Person != null ? r.PersonAlias.Person.FullNameReversed : string.Format( "{0}, {1}", r.LastName, r.FirstName );
                            };

                            if ( sortProperty.Direction == SortDirection.Ascending )
                            {
                                gRegistrations.SetLinqDataSource( qry.ToList().OrderBy( sortBy ).AsQueryable() );
                            }
                            else
                            {
                                gRegistrations.SetLinqDataSource( qry.ToList().OrderByDescending( sortBy ).AsQueryable() );
                            }
                        }
                        else
                        {
                            gRegistrations.SetLinqDataSource( qry.Sort( sortProperty ) );
                        }
                    }
                    else
                    {
                        gRegistrations.SetLinqDataSource( qry.OrderByDescending( r => r.CreatedDateTime ) );
                    }

                    // Get all the payments for any registrations being displayed on the current page.
                    // This is used in the RowDataBound event but queried now so that each row does
                    // not have to query for the data.
                    var currentPageRegistrations = gRegistrations.DataSource as List<Registration>;
                    if ( currentPageRegistrations != null && registrationEntityType != null )
                    {
                        var registrationIds = currentPageRegistrations
                            .Select( r => r.Id )
                            .ToList();

                        registrationPayments = new FinancialTransactionDetailService( rockContext )
                            .Queryable().AsNoTracking()
                            .Where( d =>
                                d.EntityTypeId.HasValue &&
                                d.EntityId.HasValue &&
                                d.EntityTypeId.Value == registrationEntityType.Id &&
                                registrationIds.Contains( d.EntityId.Value ) )
                            .ToList();
                    }

                    var discountCodeHeader = gRegistrations.GetColumnByHeaderText( "Discount Code" );
                    if ( discountCodeHeader != null )
                    {
                        discountCodeHeader.Visible = GetAttributeValue( AttributeKey.DisplayDiscountCodes ).AsBoolean();
                    }

                    gRegistrations.DataBind();
                }
            }
        }

        /// <summary>
        /// Add all of the columns to the Registrations grid after the Registrants column.
        /// The Column.Insert method does not play well with buttons.
        /// </summary>
        /// <param name="setValues">if set to <c>true</c> [set values].</param>
        private void RegistrationsTabAddDynamicControls( bool setValues )
        {
            var registrantsField = gRegistrations.ColumnsOfType<RockTemplateField>().FirstOrDefault( a => a.HeaderText == "Registrants" );
            int registrantsFieldIndex = gRegistrations.Columns.IndexOf( registrantsField );

            // Remove all columns to the right of Registrants
            for ( int i = registrantsFieldIndex + 2; i < gRegistrations.Columns.Count; i++ )
            {
                gRegistrations.Columns.RemoveAt( i );
            }

            // Add Attribute columns if necessary
            if ( AvailableRegistrationAttributesForGrid != null )
            {
                foreach ( var attributeCache in AvailableRegistrationAttributesForGrid )
                {
                    bool columnExists = gRegistrations.Columns.OfType<AttributeField>().FirstOrDefault( a => a.AttributeId == attributeCache.Id ) != null;
                    if ( !columnExists )
                    {
                        AttributeField boundField = new AttributeField();
                        boundField.DataField = attributeCache.Key;
                        boundField.AttributeId = attributeCache.Id;
                        boundField.HeaderText = attributeCache.Name;
                        boundField.ItemStyle.HorizontalAlign = attributeCache.FieldType.Field.AlignValue;
                        gRegistrations.Columns.Add( boundField );
                    }
                }
            }

            // Add the rest of the columns
            var dtWhen = new DateTimeField { DataField = "CreatedDateTime", HeaderText = "When", SortExpression = "CreatedDateTime" };
            dtWhen.HeaderStyle.HorizontalAlign = HorizontalAlign.Left;
            dtWhen.ItemStyle.HorizontalAlign = HorizontalAlign.Left;
            gRegistrations.Columns.Add( dtWhen );

            var lDiscount = new RockLiteralField { ID = "lDiscount", HeaderText = "Discount Code", SortExpression = "DiscountCode", Visible = false };
            lDiscount.HeaderStyle.HorizontalAlign = HorizontalAlign.Left;
            lDiscount.ItemStyle.HorizontalAlign = HorizontalAlign.Left;
            gRegistrations.Columns.Add( lDiscount );

            var lRegistrationCost = new RockLiteralField { ID = "lRegistrationCost", HeaderText = "Total Cost", SortExpression = "TotalCost" };
            lRegistrationCost.HeaderStyle.HorizontalAlign = HorizontalAlign.Right;
            lRegistrationCost.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gRegistrations.Columns.Add( lRegistrationCost );

            var lBalance = new RockLiteralField { ID = "lBalance", HeaderText = "Balance Due", SortExpression = "BalanceDue" };
            lBalance.HeaderStyle.HorizontalAlign = HorizontalAlign.Right;
            lBalance.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gRegistrations.Columns.Add( lBalance );

            DeleteField deleteField = new DeleteField();
            deleteField.Click += gRegistrations_Delete;
            gRegistrations.Columns.Add( deleteField );
        }

        #endregion

        #region ViewModel

        /// <summary>
        /// Manages state for the RegistrationInstance:RegistrationList block.
        /// </summary>
        internal class RegistrationInstanceRegistrationListViewModel : RegistrationInstanceViewModelBase
        {
            public bool HasData { get; set; }

            public string BlockTitle { get; set; }
        }

        #endregion

        #region Shared Components

        /// <summary>
        /// Manages the settings for a block as key/value pairs in a nominated dictionary.
        /// To share context with other blocks on a page, specify the HttpRequest.Context as the context dictionary.
        /// </summary>
        /// <remarks>
        /// This class is designed to be a reusable component.
        /// It is implemented separately in each block instance to allow file-copy deployment of blocks in a web-site project environment.
        /// Last Updated: 2020-31-01 (DL)
        /// </remarks>
        internal class BlockContextManager
        {
            /*
             * Context-related methods taken from the RockPage class, reimplemented to be independent of the Page object.
             * The RockPage implementation is tied to the HttpRequest context dictionary, whereas this implementation uses an injected dictionary.
             */

            /// <summary>
            /// Initialize a new instance of the BlockContext class
            /// </summary>
            /// <param name="contextKeyPrefix">A unique identifier of the scope for this context.</param>
            /// <param name="contextDictionary"></param>
            public BlockContextManager( string contextKeyPrefix, System.Collections.IDictionary contextDictionary )
            {
                this.ContextKeyPrefix = contextKeyPrefix;
                this.ContextDictionary = contextDictionary;
            }

            /// <summary>
            /// Gets the context dictionary for this block.
            /// To share context with other blocks on the same page, set this property to HttpRequest.Context.
            /// </summary>
            public System.Collections.IDictionary ContextDictionary { get; set; }
            public string ContextKeyPrefix;

            /// <summary>
            /// Used to save an item to the current HTTPRequests items collection.  This is useful if multiple blocks
            /// on the same page will need access to the same object.  The first block can read the object and save
            /// it using this method for the other blocks to reference
            /// </summary>
            /// <param name="key">A <see cref="System.String"/> representing the item's key</param>
            /// <param name="item">The <see cref="System.Object"/> to save.</param>
            public void SetItem( string key, object item )
            {
                key = ContextKeyPrefix + key;

                if ( ContextDictionary.Contains( key ) )
                {
                    ContextDictionary[key] = item;
                }
                else
                {
                    ContextDictionary.Add( key, item );
                }
            }

            /// <summary>
            /// Retrieves an item from the context dictionary by key.
            /// To share context with other blocks on the same page, inject the HttpRequest context.
            /// </summary>
            /// <param name="key">A <see cref="System.String"/> representing the object's key value.</param>
            /// <returns>The shared <see cref="System.Object"/>, if a match for the key is not found, a null value will be returned.</returns>
            public object GetItem( string key )
            {
                key = ContextKeyPrefix + key;

                if ( ContextDictionary.Contains( key ) )
                {
                    return ContextDictionary[key];
                }

                return null;
            }
        }

        /// <summary>
        /// Provides base functionality to manage state for a RegistrationInstance block.
        /// </summary>
        /// <remarks>
        /// This class is designed to be a reusable component.
        /// It is implemented separately in each block instance to allow file-copy deployment of blocks in a web-site project environment.
        /// Last Updated: 2020/31/01-1623 (DL)
        /// </remarks>        
        internal abstract class RegistrationInstanceViewModelBase
        {
            #region Fields and Properties

            private RockContext _DataContext = new RockContext();

            /// <summary>
            /// The data context used to load the data for the view model.
            /// </summary>
            public RockContext DataContext
            {
                get
                {
                    return _DataContext;
                }
            }

            /// <summary>
            /// The registration template description suitable for display in the Event Registration Wizard navigation bar.
            /// </summary>
            public string WizardTemplateLabel { get; set; }

            /// <summary>
            /// The registration instance description suitable for display in the Event Registration Wizard navigation bar.
            /// </summary>
            public string WizardInstanceLabel { get; set; }

            public BlockContextManager ContextManager { get; set; }

            /// <summary>
            /// Does the block have data to display?
            /// </summary>
            public bool HasData { get; set; }

            /// <summary>
            /// The text displayed in the title bar of the block.
            /// </summary>
            public string BlockTitle { get; set; }

            /// <summary>
            /// Does the current user have Edit permission for the View/Block?
            /// </summary>
            public bool UserHasEditPermissionForView { get; set; }

            /// <summary>
            /// Is the view in read-only mode?
            /// </summary>
            public bool IsReadOnly { get; set; }

            /// <summary>
            /// The active Registration Instance.
            /// </summary>
            public RegistrationInstance RegistrationInstance { get; set; }

            /// <summary>
            /// The unique identifier of the active Registration Instance.
            /// </summary>
            public int? RegistrationInstanceId { get; set; }

            /// <summary>
            /// The unique identifier of the Registration Template associated with the active Registration Instance.
            /// </summary>
            public int? RegistrationTemplateId { get; set; }

            /// <summary>
            /// The unique identifier of the default account for payments associated with this Registration Instance.
            /// </summary>
            public Guid? DefaultAccountGuid { get; set; }

            /// <summary>
            /// Does this registration instance have associated payments?
            /// </summary>
            public bool HasPayments { get; set; }
            /// <summary>
            /// Does this registration instance have associated fees or costs?
            /// </summary>
            public bool HasCosts { get; set; }

            #endregion

            /// <summary>
            /// Gets the registration instance.
            /// </summary>
            /// <param name="registrationInstanceId">The registration instance identifier.</param>
            /// <param name="rockContext">The rock context.</param>
            /// <returns></returns>
            internal RegistrationInstance GetRegistrationInstance( int registrationInstanceId )
            {
                string key = string.Format( "RegistrationInstance:{0}", registrationInstanceId );

                var registrationInstance = this.ContextManager.GetItem( key ) as RegistrationInstance;

                if ( registrationInstance == null )
                {
                    registrationInstance = new RegistrationInstanceService( _DataContext )
                        .Queryable( "RegistrationTemplate,Account,RegistrationTemplate.Forms.Fields" )
                        .AsNoTracking()
                        .FirstOrDefault( i => i.Id == registrationInstanceId );

                    this.ContextManager.SetItem( key, registrationInstance );
                }

                return registrationInstance;
            }

            /// <summary>
            /// Load the registration instance data, but do not populate the display properties.
            /// Use this method to load data for postback processing.
            /// </summary>
            /// <param name="registrationInstanceId"></param>
            /// <param name="parentTemplateId"></param>
            public void LoadRegistrationInstance( int? registrationInstanceId, int? parentTemplateId = null )
            {
                RegistrationInstance registrationInstance = null;

                if ( registrationInstanceId.HasValue )
                {
                    registrationInstance = GetRegistrationInstance( registrationInstanceId.Value );
                }

                if ( registrationInstance == null )
                {
                    registrationInstance = new RegistrationInstance();
                    registrationInstance.Id = 0;
                    registrationInstance.IsActive = true;
                    registrationInstance.RegistrationTemplateId = parentTemplateId ?? 0;

                    if ( this.DefaultAccountGuid.HasValue )
                    {
                        var account = new FinancialAccountService( _DataContext ).Get( this.DefaultAccountGuid.Value );
                        registrationInstance.AccountId = account != null ? account.Id : 0;
                    }
                }

                if ( registrationInstance.RegistrationTemplate == null
                     && registrationInstance.RegistrationTemplateId > 0 )
                {
                    registrationInstance.RegistrationTemplate = new RegistrationTemplateService( _DataContext )
                        .Get( registrationInstance.RegistrationTemplateId );
                }

                // Check if this Registration Instance has associated costs or fees.
                this.HasCosts = ( ( registrationInstance.RegistrationTemplate.SetCostOnInstance.HasValue && registrationInstance.RegistrationTemplate.SetCostOnInstance == true && registrationInstance.Cost.HasValue && registrationInstance.Cost.Value > 0 )
                    || registrationInstance.RegistrationTemplate.Cost > 0
                    || registrationInstance.RegistrationTemplate.Fees.Count > 0 );

                // Check if this Registration Instance has associated payments.
                var registrationIdQry = new RegistrationService( _DataContext )
                    .Queryable().AsNoTracking()
                    .Where( r =>
                        r.RegistrationInstanceId == registrationInstanceId &&
                        !r.IsTemporary )
                    .Select( r => r.Id );

                var registrationEntityType = EntityTypeCache.Get( typeof( Rock.Model.Registration ) );

                this.HasPayments = new FinancialTransactionDetailService( _DataContext )
                    .Queryable().AsNoTracking()
                    .Where( d =>
                        d.EntityTypeId.HasValue &&
                        d.EntityId.HasValue &&
                        d.EntityTypeId.Value == registrationEntityType.Id &&
                        registrationIdQry.Contains( d.EntityId.Value ) )
                    .Any();
            }
        }

        #endregion
    }

}