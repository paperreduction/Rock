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

using Newtonsoft.Json;

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
    /// A Block that displays the list of Registrants related to a Registration Instance.
    /// </summary>
    [DisplayName( "Registration Instance - Registrant List" )]
    [Category( "Event" )]
    [Description( "Displays the list of Registrants related to a Registration Instance." )]
    [AccountField( "Default Account", "The default account to use for new registration instances", false, Rock.SystemGuid.FinancialAccount.. "2A6F9E5F-6859-44F1-AB0E-CE9CF6B08EE5", "", 0 )]
    [LinkedPage( "Registration Page", "The page for editing registration and registrant information", true, "", "", 1 )]
/*
    [LinkedPage( "Linkage Page", "The page for editing registration linkages", true, "", "", 2 )]
    [LinkedPage( "Calendar Item Page", "The page to view calendar item details", true, "", "", 3 )]
    [LinkedPage( "Group Detail Page", "The page for viewing details about a group", true, "", "", 4 )]
    [LinkedPage( "Content Item Page", "The page for viewing details about a content channel item", true, "", "", 5 )]
    [LinkedPage( "Transaction Detail Page", "The page for viewing details about a payment", true, "", "", 6 )]
    [LinkedPage( "Payment Reminder Page", "The page for manually sending payment reminders.", false, "", "", 7 )]
    [LinkedPage( "Wait List Process Page", "The page for moving a person from the wait list to a full registrant.", true, "", "", 8 )]
    [BooleanField( "Display Discount Codes", "Display the discount code used with a payment", false, "", 9 )]
*/
    public partial class RegistrationInstanceRegistrantList : Rock.Web.UI.RockBlock //, IDetailBlock
    {
        #region Fields

        private Dictionary<int, Location> _homeAddresses = new Dictionary<int, Location>();
        private Dictionary<int, PhoneNumber> _mobilePhoneNumbers = new Dictionary<int, PhoneNumber>();
        private Dictionary<int, PhoneNumber> _homePhoneNumbers = new Dictionary<int, PhoneNumber>();
        private bool _isExporting = false;

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
        /// Gets or sets the registrant form fields that were configured as 'Show on Grid' for the registration template
        /// </summary>
        /// <value>
        /// The registrant fields.
        /// </value>
        public List<RegistrantFormField> RegistrantFields { get; set; }

        /// <summary>
        /// Gets or sets the person campus ids.
        /// </summary>
        /// <value>
        /// The person campus ids.
        /// </value>
        private Dictionary<int, List<int>> PersonCampusIds { get; set; }

        /// <summary>
        /// Gets or sets the signed person ids.
        /// </summary>
        /// <value>
        /// The signed person ids.
        /// </value>
        private List<int> Signers { get; set; }

        /// <summary>
        /// Gets or sets the group links.
        /// </summary>
        /// <value>
        /// The group links.
        /// </value>
        private Dictionary<int, string> GroupLinks { get; set; }

        /// <summary>
        /// Gets or sets the active tab.
        /// </summary>
        /// <value>
        /// The active tab.
        /// </value>
        protected string ActiveTab { get; set; }

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

            ActiveTab = ( ViewState["ActiveTab"] as string ) ?? string.Empty;
            RegistrantFields = ViewState["RegistrantFields"] as List<RegistrantFormField>;
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

            ddlRegistrantsInGroup.Items.Clear();
            ddlRegistrantsInGroup.Items.Add( new ListItem());
            ddlRegistrantsInGroup.Items.Add( new ListItem( "Yes", "Yes" ) );
            ddlRegistrantsInGroup.Items.Add( new ListItem( "No", "No" ) );

            ddlRegistrantsSignedDocument.Items.Clear();
            ddlRegistrantsSignedDocument.Items.Add( new ListItem() );
            ddlRegistrantsSignedDocument.Items.Add( new ListItem( "Yes", "Yes" ) );
            ddlRegistrantsSignedDocument.Items.Add( new ListItem( "No", "No" ) );

            fRegistrants.ApplyFilterClick += fRegistrants_ApplyFilterClick;
            gRegistrants.DataKeyNames = new string[] { "Id" };
            gRegistrants.Actions.ShowAdd = true;
            gRegistrants.Actions.AddClick += gRegistrants_AddClick;
            gRegistrants.RowDataBound += gRegistrants_RowDataBound;
            gRegistrants.GridRebind += gRegistrants_GridRebind;

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
            ViewState["RegistrantFields"] = RegistrantFields;
            ViewState["ActiveTab"] = ActiveTab;
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

            int? registrationInstanceId = PageParameter( pageReference, "RegistrationInstanceId" ).AsIntegerOrNull();
            if ( registrationInstanceId.HasValue )
            {
                RegistrationInstance registrationInstance = GetRegistrationInstance( registrationInstanceId.Value );
                if ( registrationInstance != null )
                {
                    breadCrumbs.Add( new BreadCrumb( registrationInstance.ToString(), pageReference ) );
                    return breadCrumbs;
                }
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
            RegistrantsTabAddDynamicControls( setValues );
        }

        /// <summary>
        /// Handles the Click event of the btnDelete control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnDelete_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var service = new RegistrationInstanceService( rockContext );
                var registrationInstance = service.Get( hfRegistrationInstanceId.Value.AsInteger() );

                if ( registrationInstance != null )
                {
                    int registrationTemplateId = registrationInstance.RegistrationTemplateId;

                    if ( UserCanEdit ||
                         registrationInstance.IsAuthorized( Authorization.EDIT, CurrentPerson ) ||
                         registrationInstance.IsAuthorized( Authorization.ADMINISTRATE, this.CurrentPerson ) )
                    {
                        rockContext.WrapTransaction( () =>
                        {
                            new RegistrationService( rockContext ).DeleteRange( registrationInstance.Registrations );
                            service.Delete( registrationInstance );
                            rockContext.SaveChanges();
                        } );

                        var qryParams = new Dictionary<string, string> { { "RegistrationTemplateId", registrationTemplateId.ToString() } };
                        NavigateToParentPage( qryParams );
                    }
                    else
                    {
                        //mdDeleteWarning.Show( "You are not authorized to delete this registration instance.", ModalAlertType.Information );
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btnPreview control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnPreview_Click( object sender, EventArgs e )
        {
        }

        /// <summary>
        /// Handles the Click event of the btnSendPaymentReminder control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnSendPaymentReminder_Click( object sender, EventArgs e )
        {
            Dictionary<string, string> queryParms = new Dictionary<string, string>();
            queryParms.Add( "RegistrationInstanceId", PageParameter( "RegistrationInstanceId" ) );
            NavigateToLinkedPage( "PaymentReminderPage", queryParms );
        }

        #endregion

        #region Registrant Tab Events

        /// <summary>
        /// Handles the ApplyFilterClick event of the fRegistrants control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void fRegistrants_ApplyFilterClick( object sender, EventArgs e )
        {
            fRegistrants.SaveUserPreference( "Registrants Date Range", "Registration Date Range",  sdrpRegistrantsRegistrantDateRange.DelimitedValues );
            fRegistrants.SaveUserPreference( "First Name", tbRegistrantsRegistrantFirstName.Text );
            fRegistrants.SaveUserPreference( "Last Name", tbRegistrantsRegistrantLastName.Text );
            fRegistrants.SaveUserPreference( "In Group", ddlRegistrantsInGroup.SelectedValue );
            fRegistrants.SaveUserPreference( "Signed Document", ddlRegistrantsSignedDocument.SelectedValue );

            if ( RegistrantFields != null )
            {
                foreach ( var field in RegistrantFields )
                {
                    if ( field.FieldSource == RegistrationFieldSource.PersonField && field.PersonFieldType.HasValue )
                    {
                        switch ( field.PersonFieldType.Value )
                        {
                            case RegistrationPersonFieldType.Campus:
                                var ddlCampus = phRegistrantsRegistrantFormFieldFilters.FindControl( "ddlRegistrantsCampus" ) as RockDropDownList;
                                if ( ddlCampus != null )
                                {
                                    fRegistrants.SaveUserPreference( "Home Campus", ddlCampus.SelectedValue );
                                }

                                break;

                            case RegistrationPersonFieldType.Email:
                                var tbEmailFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsEmailFilter" ) as RockTextBox;
                                if ( tbEmailFilter != null )
                                {
                                    fRegistrants.SaveUserPreference( "Email", tbEmailFilter.Text );
                                }

                                break;

                            case RegistrationPersonFieldType.Birthdate:
                                var drpBirthdateFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "drpRegistrantsBirthdateFilter" ) as DateRangePicker;
                                if ( drpBirthdateFilter != null )
                                {
                                    fRegistrants.SaveUserPreference( "Birthdate Range", drpBirthdateFilter.DelimitedValues );
                                }

                                break;
                            case RegistrationPersonFieldType.MiddleName:
                                var tbMiddleNameFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsMiddleNameFilter" ) as RockTextBox;
                                if ( tbMiddleNameFilter != null )
                                {
                                    fRegistrants.SaveUserPreference( "MiddleName", tbMiddleNameFilter.Text );
                                }
                                break;
                            case RegistrationPersonFieldType.AnniversaryDate:
                                var drAnniversaryDateFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "drpRegistrantsAnniversaryDateFilter" ) as DateRangePicker;
                                if ( drAnniversaryDateFilter != null )
                                {
                                    fRegistrants.SaveUserPreference( "AnniversaryDate Range", drAnniversaryDateFilter.DelimitedValues );
                                }
                                break;
                            case RegistrationPersonFieldType.Grade:
                                var gpGradeFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "gpRegistrantsGradeFilter" ) as GradePicker;
                                if ( gpGradeFilter != null )
                                {
                                    int? gradeOffset = gpGradeFilter.SelectedValueAsInt( false );
                                    fRegistrants.SaveUserPreference( "Grade", gradeOffset.HasValue ? gradeOffset.Value.ToString() : string.Empty );
                                }

                                break;
                                
                            case RegistrationPersonFieldType.Gender:
                                var ddlGenderFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "ddlRegistrantsGenderFilter" ) as RockDropDownList;
                                if ( ddlGenderFilter != null )
                                {
                                    fRegistrants.SaveUserPreference( "Gender", ddlGenderFilter.SelectedValue );
                                }

                                break;

                            case RegistrationPersonFieldType.MaritalStatus:
                                var dvpMaritalStatusFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "dvpRegistrantsMaritalStatusFilter" ) as DefinedValuePicker;
                                if ( dvpMaritalStatusFilter != null )
                                {
                                    fRegistrants.SaveUserPreference( "Marital Status", dvpMaritalStatusFilter.SelectedValue );
                                }

                                break;
                                
                            case RegistrationPersonFieldType.MobilePhone:
                                var tbMobilePhoneFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsMobilePhoneFilter" ) as RockTextBox;
                                if ( tbMobilePhoneFilter != null )
                                {
                                    fRegistrants.SaveUserPreference( "Cell Phone", "Cell Phone", tbMobilePhoneFilter.Text );
                                }

                                break;

                            case RegistrationPersonFieldType.HomePhone:
                                var tbRegistrantsHomePhoneFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsHomePhoneFilter" ) as RockTextBox;
                                if ( tbRegistrantsHomePhoneFilter != null )
                                {
                                    fRegistrants.SaveUserPreference( "Home Phone", tbRegistrantsHomePhoneFilter.Text );
                                }

                                break;
                        }
                    }

                    if ( field.Attribute != null )
                    {
                        var attribute = field.Attribute;
                        var filterControl = phRegistrantsRegistrantFormFieldFilters.FindControl( "filterRegistrants_" + attribute.Id.ToString() );
                        if ( filterControl != null )
                        {
                            try
                            {
                                var values = attribute.FieldType.Field.GetFilterValues( filterControl, field.Attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter );
                                fRegistrants.SaveUserPreference( attribute.Key, attribute.Name, attribute.FieldType.Field.GetFilterValues( filterControl, attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter ).ToJson() );
                            }
                            catch { }
                        }
                    }
                }
            }

            BindRegistrantsGrid();
        }

        /// <summary>
        /// Handles the ClearFilterClick event of the fRegistrants control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void fRegistrants_ClearFilterClick( object sender, EventArgs e )
        {
            fRegistrants.DeleteUserPreferences();

            foreach ( var control in phRegistrantsRegistrantFormFieldFilters.ControlsOfTypeRecursive<Control>().Where( a => a.ID != null && a.ID.StartsWith( "filter" ) && a.ID.Contains( "_" ) ) )
            {
                var attributeId = control.ID.Split( '_' )[1].AsInteger();
                var attribute = AttributeCache.Get( attributeId );
                if ( attribute != null )
                {
                    attribute.FieldType.Field.SetFilterValues( control, attribute.QualifierValues, new List<string>() );
                }
            }

            if ( RegistrantFields != null )
            {
                foreach ( var field in RegistrantFields )
                {
                    if ( field.FieldSource == RegistrationFieldSource.PersonField && field.PersonFieldType.HasValue )
                    {
                        switch ( field.PersonFieldType.Value )
                        {
                            case RegistrationPersonFieldType.Campus:
                                var ddlCampus = phRegistrantsRegistrantFormFieldFilters.FindControl( "ddlRegistrantsCampus" ) as RockDropDownList;
                                if ( ddlCampus != null )
                                {
                                    ddlCampus.SetValue( ( Guid? ) null );
                                }

                                break;

                            case RegistrationPersonFieldType.Email:
                                var tbEmailFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsEmailFilter" ) as RockTextBox;
                                if ( tbEmailFilter != null )
                                {
                                    tbEmailFilter.Text = string.Empty;
                                }

                                break;

                            case RegistrationPersonFieldType.Birthdate:
                                var drpBirthdateFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "drpRegistrantsBirthdateFilter" ) as DateRangePicker;
                                if ( drpBirthdateFilter != null )
                                {
                                    drpBirthdateFilter.LowerValue = null;
                                    drpBirthdateFilter.UpperValue = null;
                                }

                                break;
                            case RegistrationPersonFieldType.MiddleName:
                                var tbMiddleNameFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsMiddleNameFilter" ) as RockTextBox;
                                if ( tbMiddleNameFilter != null )
                                {
                                    tbMiddleNameFilter.Text = string.Empty;
                                }
                                break;
                            case RegistrationPersonFieldType.AnniversaryDate:
                                var drAnniversaryDateFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "drpRegistrantsAnniversaryDateFilter" ) as DateRangePicker;
                                if ( drAnniversaryDateFilter != null )
                                {
                                    drAnniversaryDateFilter.LowerValue = null;
                                    drAnniversaryDateFilter.UpperValue = null;
                                }
                                break;
                            case RegistrationPersonFieldType.Grade:
                                var gpGradeFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "gpRegistrantsGradeFilter" ) as GradePicker;
                                if ( gpGradeFilter != null )
                                {
                                    gpGradeFilter.SetValue( ( Guid? ) null );
                                }

                                break;
                                
                            case RegistrationPersonFieldType.Gender:
                                var ddlGenderFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "ddlRegistrantsGenderFilter" ) as RockDropDownList;
                                if ( ddlGenderFilter != null )
                                {
                                    ddlGenderFilter.SetValue( ( Guid? ) null );
                                }

                                break;

                            case RegistrationPersonFieldType.MaritalStatus:
                                var dvpMaritalStatusFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "dvpRegistrantsMaritalStatusFilter" ) as DefinedValuePicker;
                                if ( dvpMaritalStatusFilter != null )
                                {
                                    dvpMaritalStatusFilter.SetValue( ( Guid? ) null );
                                }

                                break;
                                
                            case RegistrationPersonFieldType.MobilePhone:
                                var tbMobilePhoneFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsMobilePhoneFilter" ) as RockTextBox;
                                if ( tbMobilePhoneFilter != null )
                                {
                                    tbMobilePhoneFilter.Text = string.Empty;
                                }

                                break;

                            case RegistrationPersonFieldType.HomePhone:
                                var tbRegistrantsHomePhoneFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsHomePhoneFilter" ) as RockTextBox;
                                if ( tbRegistrantsHomePhoneFilter != null )
                                {
                                    tbRegistrantsHomePhoneFilter.Text = string.Empty;
                                }

                                break;
                        }
                    }
                }
            }

            BindRegistrantsFilter( null );
        }

        /// <summary>
        /// Fs the registrants_ display filter value.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        protected void fRegistrants_DisplayFilterValue( object sender, GridFilter.DisplayFilterValueArgs e )
        {
            if ( RegistrantFields != null )
            {
                var attribute = RegistrantFields
                    .Where( a =>
                        a.Attribute != null &&
                        a.Attribute.Key == e.Key )
                    .Select( a => a.Attribute )
                    .FirstOrDefault();

                if ( attribute != null )
                {
                    try
                    {
                        var values = JsonConvert.DeserializeObject<List<string>>( e.Value );
                        e.Value = attribute.FieldType.Field.FormatFilterValues( attribute.QualifierValues, values );
                        return;
                    }
                    catch { }
                }
            }

            switch ( e.Key )
            {
                case "Registrants Date Range":
                    e.Value = SlidingDateRangePicker.FormatDelimitedValues( e.Value );
                    break;
                    
                case "Birthdate Range":
                    // The value might either be from a SlidingDateRangePicker or a DateRangePicker, so try both
                    var storedValue = e.Value;
                    e.Value = SlidingDateRangePicker.FormatDelimitedValues( storedValue );
                    if ( e.Value.IsNullOrWhiteSpace() )
                    {
                        e.Value = DateRangePicker.FormatDelimitedValues( storedValue );
                    }

                    break;
                    
                case "Grade":
                    e.Value = Person.GradeFormattedFromGradeOffset( e.Value.AsIntegerOrNull() );
                    break;
                    
                case "First Name":
                case "Last Name":
                case "Email":
                case "Signed Document":
                case "Home Phone":
                case "Cell Phone":
                    break;
                    
                case "Gender":
                    var gender = e.Value.ConvertToEnumOrNull<Gender>();
                    e.Value = gender.HasValue ? gender.ConvertToString() : string.Empty;
                    break;
                    
                case "Campus":
                    int? campusId = e.Value.AsIntegerOrNull();
                    if ( campusId.HasValue )
                    {
                        var campus = CampusCache.Get( campusId.Value );
                        e.Value = campus != null ? campus.Name : string.Empty;
                    }
                    else
                    {
                        e.Value = string.Empty;
                    }

                    break;
                    
                case "Marital Status":
                    int? dvId = e.Value.AsIntegerOrNull();
                    if ( dvId.HasValue )
                    {
                        var maritalStatus = DefinedValueCache.Get( dvId.Value );
                        e.Value = maritalStatus != null ? maritalStatus.Value : string.Empty;
                    }
                    else
                    {
                        e.Value = string.Empty;
                    }

                    break;
                    
                case "In Group":
                        e.Value = e.Value;
                        break;

                default:
                        e.Value = string.Empty;
                        break;
            }
        }

        /// <summary>
        /// Handles the GridRebind event of the gRegistrants control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void gRegistrants_GridRebind( object sender, GridRebindEventArgs e )
        {
            var registrationInstanceId = hfRegistrationInstanceId.Value.AsInteger();

            var registrationInstance = GetRegistrationInstance( registrationInstanceId );

            var name = registrationInstance.Name.FormatAsHtmlTitle();

            gRegistrants.ExportTitleName = name + " - Registrants";
            gRegistrants.ExportFilename = gRegistrants.ExportFilename ?? name + "Registrants";
            BindRegistrantsGrid( e.IsExporting );
        }

        /// <summary>
        /// Handles the RowDataBound event of the gRegistrants control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridViewRowEventArgs"/> instance containing the event data.</param>
        private void gRegistrants_RowDataBound( object sender, GridViewRowEventArgs e )
        {
            var registrant = e.Row.DataItem as RegistrationRegistrant;
            if ( registrant != null )
            {
                // Set the registrant name value
                var lRegistrant = e.Row.FindControl( "lRegistrant" ) as Literal;
                if ( lRegistrant != null )
                {
                    if ( registrant.PersonAlias != null && registrant.PersonAlias.Person != null )
                    {
                        lRegistrant.Text = registrant.PersonAlias.Person.FullNameReversed +
                            ( Signers != null && !Signers.Contains( registrant.PersonAlias.PersonId ) ? " <i class='fa fa-pencil-square-o text-danger'></i>" : string.Empty );
                    }
                    else
                    {
                        lRegistrant.Text = string.Empty;
                    }
                }

                // Set the Group Name
                if ( registrant.GroupMember != null && GroupLinks.ContainsKey( registrant.GroupMember.GroupId ) )
                {
                    var lGroup = e.Row.FindControl( "lGroup" ) as Literal;
                    if ( lGroup != null )
                    {
                        lGroup.Text = GroupLinks[registrant.GroupMember.GroupId];
                    }
                }

                // Set the campus
                var lCampus = e.Row.FindControl( "lRegistrantsCampus" ) as Literal;
                
                // if it's null, try looking for the "lGroupPlacementsCampus" control since this RowDataBound event is shared between
                // two different grids.
                if ( lCampus == null )
                {
                    lCampus = e.Row.FindControl( "lGroupPlacementsCampus" ) as Literal;
                }
                
                if ( lCampus != null && PersonCampusIds != null )
                {
                    if ( registrant.PersonAlias != null )
                    {
                        if ( PersonCampusIds.ContainsKey( registrant.PersonAlias.PersonId ) )
                        {
                            var campusIds = PersonCampusIds[registrant.PersonAlias.PersonId];
                            if ( campusIds.Any() )
                            {
                                var campusNames = new List<string>();
                                foreach ( int campusId in campusIds )
                                {
                                    var campus = CampusCache.Get( campusId );
                                    if ( campus != null )
                                    {
                                        campusNames.Add( campus.Name );
                                    }
                                }

                                lCampus.Text = campusNames.AsDelimited( "<br/>" );
                            }
                        }
                    }
                }

                // Set the Fees
                var lFees = e.Row.FindControl( "lFees" ) as Literal;
                if ( lFees != null )
                {
                    if ( registrant.Fees != null && registrant.Fees.Any() )
                    {
                        var feeDesc = new List<string>();
                        foreach ( var fee in registrant.Fees )
                        {
                            feeDesc.Add( string.Format(
                                "{0}{1} ({2})",
                                fee.Quantity > 1 ? fee.Quantity.ToString( "N0" ) + " " : string.Empty,
                                fee.Quantity > 1 ? fee.RegistrationTemplateFee.Name.Pluralize() : fee.RegistrationTemplateFee.Name,
                                fee.Cost.FormatAsCurrency() ) );
                        }

                        lFees.Text = feeDesc.AsDelimited( "<br/>" );
                    }
                }

                if ( _homeAddresses.Any() && _homeAddresses.ContainsKey( registrant.PersonId.Value ) )
                {
                    var location = _homeAddresses[registrant.PersonId.Value];
                    // break up addresses if exporting
                    if ( _isExporting )
                    {
                        var lStreet1 = e.Row.FindControl( "lStreet1" ) as Literal;
                        var lStreet2 = e.Row.FindControl( "lStreet2" ) as Literal;
                        var lCity = e.Row.FindControl( "lCity" ) as Literal;
                        var lState = e.Row.FindControl( "lState" ) as Literal;
                        var lPostalCode = e.Row.FindControl( "lPostalCode" ) as Literal;
                        var lCountry = e.Row.FindControl( "lCountry" ) as Literal;

                        if ( location != null )
                        {
                            lStreet1.Text = location.Street1;
                            lStreet2.Text = location.Street2;
                            lCity.Text = location.City;
                            lState.Text = location.State;
                            lPostalCode.Text = location.PostalCode;
                            lCountry.Text = location.Country;
                        }
                    }
                    else
                    {
                        var addressField = e.Row.FindControl( "lRegistrantsAddress" ) as Literal ?? e.Row.FindControl( "lGroupPlacementsAddress" ) as Literal;
                        if ( addressField != null )
                        {
                            addressField.Text = location != null && location.FormattedAddress.IsNotNullOrWhiteSpace() ? location.FormattedAddress : string.Empty;
                        }
                    }
                }

                if (_mobilePhoneNumbers.Any())
                {
                    var mobileNumber = _mobilePhoneNumbers[registrant.PersonId.Value];
                    var mobileField = e.Row.FindControl( "lRegistrantsMobile" ) as Literal ?? e.Row.FindControl( "lGroupPlacementsMobile" ) as Literal;
                    if ( mobileField != null)
                    {
                        if (mobileNumber == null || mobileNumber.NumberFormatted.IsNullOrWhiteSpace())
                        {
                            mobileField.Text = string.Empty;
                        }
                        else
                        {
                            mobileField.Text = mobileNumber.IsUnlisted ? "Unlisted" : mobileNumber.NumberFormatted;
                        }
                    }
                    
                }

                if ( _homePhoneNumbers.Any() )
                {
                    var homePhoneNumber = _homePhoneNumbers[registrant.PersonId.Value];
                    var homePhoneField = e.Row.FindControl( "lRegistrantsHomePhone" ) as Literal ?? e.Row.FindControl( "lGroupPlacementsHomePhone" ) as Literal;
                    if ( homePhoneField != null )
                    {
                        if ( homePhoneNumber == null || homePhoneNumber.NumberFormatted.IsNullOrWhiteSpace() )
                        {
                            homePhoneField.Text = string.Empty;
                        }
                        else
                        {
                            homePhoneField.Text = homePhoneNumber.IsUnlisted ? "Unlisted" : homePhoneNumber.NumberFormatted;
                        }
                    }

                }
            }
        }

        /// <summary>
        /// Handles the AddClick event of the gRegistrants control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gRegistrants_AddClick( object sender, EventArgs e )
        {
            NavigateToLinkedPage( "RegistrationPage", "RegistrationId", 0, "RegistrationInstanceId", hfRegistrationInstanceId.ValueAsInt() );
        }

        /// <summary>
        /// Handles the RowSelected event of the gRegistrants control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gRegistrants_RowSelected( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var registrantService = new RegistrationRegistrantService( rockContext );
                var registrant = registrantService.Get( e.RowKeyId );
                if ( registrant != null )
                {
                    var qryParams = new Dictionary<string, string>();
                    qryParams.Add( "RegistrationId", registrant.RegistrationId.ToString() );
                    string url = LinkedPageUrl( "RegistrationPage", qryParams );
                    url += "#" + e.RowKeyValue;
                    Response.Redirect( url, false );
                }
            }
        }

        /// <summary>
        /// Handles the Delete event of the gRegistrants control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gRegistrants_Delete( object sender, RowEventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var registrantService = new RegistrationRegistrantService( rockContext );
                var registrant = registrantService.Get( e.RowKeyId );
                if ( registrant != null )
                {
                    string errorMessage;
                    if ( !registrantService.CanDelete( registrant, out errorMessage ) )
                    {
                        mdRegistrantsGridWarning.Show( errorMessage, ModalAlertType.Information );
                        return;
                    }

                    registrantService.Delete( registrant );
                    rockContext.SaveChanges();
                }
            }

            BindRegistrantsGrid();
        }

        #endregion

        #endregion

        #region Methods

        #region Main Form Methods

        private RegistrationInstance _RegistrationInstance = null;

        /// <summary>
        /// Load the active Registration Instance for the current page context.
        /// </summary>
        private void InitializeActiveRegistrationInstance()
        {
            _RegistrationInstance = null;

            int? registrationInstanceId = this.PageParameter( "RegistrationInstanceId" ).AsInteger();

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
            int? registrationInstanceId = PageParameter( "RegistrationInstanceId" ).AsIntegerOrNull();
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

                pnlDetails.Visible = true;
                hfRegistrationInstanceId.Value = registrationInstance.Id.ToString();
                hfRegistrationTemplateId.Value = registrationInstance.RegistrationTemplateId.ToString();
                RegistrationTemplateId = registrationInstance.RegistrationTemplateId;

                LoadRegistrantFormFields( registrationInstance );
                SetUserPreferencePrefix( hfRegistrationTemplateId.ValueAsInt() );

                BindRegistrantsFilter( registrationInstance );
                AddDynamicControls( true );

                BindRegistrantsGrid();
            }
        }

        /// <summary>
        /// Sets the user preference prefix.
        /// </summary>
        private void SetUserPreferencePrefix(int registrationTemplateId )
        {
            fRegistrants.UserPreferenceKeyPrefix = string.Format( "{0}-", registrationTemplateId );
        }

        #endregion

        #region Registrants Tab

        /// <summary>
        /// Binds the registrants filter.
        /// </summary>
        private void BindRegistrantsFilter( RegistrationInstance instance )
        {
            sdrpRegistrantsRegistrantDateRange.DelimitedValues = fRegistrants.GetUserPreference( "Registrants Date Range" );
            tbRegistrantsRegistrantFirstName.Text = fRegistrants.GetUserPreference( "First Name" );
            tbRegistrantsRegistrantLastName.Text = fRegistrants.GetUserPreference( "Last Name" );
            ddlRegistrantsInGroup.SetValue( fRegistrants.GetUserPreference( "In Group" ) );

            ddlRegistrantsSignedDocument.SetValue( fRegistrants.GetUserPreference( "Signed Document" ) );
            ddlRegistrantsSignedDocument.Visible = instance != null && instance.RegistrationTemplate != null && instance.RegistrationTemplate.RequiredSignatureDocumentTemplateId.HasValue;
        }

        /// <summary>
        /// Binds the registrants grid.
        /// </summary>
        private void BindRegistrantsGrid( bool isExporting = false )
        {
            _isExporting = isExporting;
            int? instanceId = hfRegistrationInstanceId.Value.AsIntegerOrNull();
            if ( instanceId.HasValue )
            {
                using ( var rockContext = new RockContext() )
                {
                    var registrationInstance = new RegistrationInstanceService( rockContext ).Get( instanceId.Value );

                    if ( registrationInstance != null &&
                        registrationInstance.RegistrationTemplate != null &&
                        registrationInstance.RegistrationTemplate.RequiredSignatureDocumentTemplateId.HasValue )
                    {
                        Signers = new SignatureDocumentService( rockContext )
                            .Queryable().AsNoTracking()
                            .Where( d =>
                                d.SignatureDocumentTemplateId == registrationInstance.RegistrationTemplate.RequiredSignatureDocumentTemplateId.Value &&
                                d.Status == SignatureDocumentStatus.Signed &&
                                d.BinaryFileId.HasValue &&
                                d.AppliesToPersonAlias != null )
                            .OrderByDescending( d => d.LastStatusDate )
                            .Select( d => d.AppliesToPersonAlias.PersonId )
                            .ToList();
                    }

                    // Start query for registrants
                    var registrationRegistrantService =  new RegistrationRegistrantService( rockContext );
                    var qry = registrationRegistrantService
                    .Queryable( "PersonAlias.Person.PhoneNumbers.NumberTypeValue,Fees.RegistrationTemplateFee,GroupMember.Group" ).AsNoTracking()
                    .Where( r =>
                        r.Registration.RegistrationInstanceId == instanceId.Value &&
                        r.PersonAlias != null &&
                        r.PersonAlias.Person != null &&
                        r.OnWaitList == false );

                    // Filter by daterange
                    var dateRange = SlidingDateRangePicker.CalculateDateRangeFromDelimitedValues( sdrpRegistrantsRegistrantDateRange.DelimitedValues );
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

                    // Filter by first name
                    if ( !string.IsNullOrWhiteSpace( tbRegistrantsRegistrantFirstName.Text ) )
                    {
                        string rfname = tbRegistrantsRegistrantFirstName.Text;
                        qry = qry.Where( r =>
                            r.PersonAlias.Person.NickName.StartsWith( rfname ) ||
                            r.PersonAlias.Person.FirstName.StartsWith( rfname ) );
                    }

                    // Filter by last name
                    if ( !string.IsNullOrWhiteSpace( tbRegistrantsRegistrantLastName.Text ) )
                    {
                        string rlname = tbRegistrantsRegistrantLastName.Text;
                        qry = qry.Where( r =>
                            r.PersonAlias.Person.LastName.StartsWith( rlname ) );
                    }

                    // Filter by signed documents
                    if ( Signers != null )
                    {
                        if ( ddlRegistrantsSignedDocument.SelectedValue.AsBooleanOrNull() == true )
                        {
                            qry = qry.Where( r => Signers.Contains( r.PersonAlias.PersonId ) );
                        }
                        else if ( ddlRegistrantsSignedDocument.SelectedValue.AsBooleanOrNull() == false )
                        {
                            qry = qry.Where( r => !Signers.Contains( r.PersonAlias.PersonId ) );
                        }
                    }

                    if ( ddlRegistrantsInGroup.SelectedValue.AsBooleanOrNull() == true )
                    {
                        qry = qry.Where( r => r.GroupMemberId.HasValue );
                    }
                    else if ( ddlRegistrantsInGroup.SelectedValue.AsBooleanOrNull() == false )
                    {
                        qry = qry.Where( r => !r.GroupMemberId.HasValue );
                    }

                    bool preloadCampusValues = false;
                    var registrantAttributes = new List<AttributeCache>();
                    var personAttributes = new List<AttributeCache>();
                    var groupMemberAttributes = new List<AttributeCache>();
                    var registrantAttributeIds = new List<int>();
                    var personAttributesIds = new List<int>();
                    var groupMemberAttributesIds = new List<int>();


                    var personIds = qry.Select( r => r.PersonAlias.PersonId ).Distinct().ToList();

                    if ( isExporting || RegistrantFields != null && RegistrantFields.Any(f => f.PersonFieldType != null && f.PersonFieldType == RegistrationPersonFieldType.Address) )
                    {
                        _homeAddresses = Person.GetHomeLocations( personIds );
                    }

                    if ( RegistrantFields != null )
                    {
                        SetPhoneDictionary( rockContext, personIds );

                        // Filter by any selected
                        foreach ( var personFieldType in RegistrantFields
                            .Where( f =>
                                f.FieldSource == RegistrationFieldSource.PersonField &&
                                f.PersonFieldType.HasValue )
                            .Select( f => f.PersonFieldType.Value ) )
                        {
                            switch ( personFieldType )
                            {
                                case RegistrationPersonFieldType.Campus:
                                    preloadCampusValues = true;

                                    var ddlCampus = phRegistrantsRegistrantFormFieldFilters.FindControl( "ddlRegistrantsCampus" ) as RockDropDownList;
                                    if ( ddlCampus != null )
                                    {
                                        var campusId = ddlCampus.SelectedValue.AsIntegerOrNull();
                                        if ( campusId.HasValue )
                                        {
                                            var familyGroupTypeGuid = Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid();
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.Members.Any( m =>
                                                    m.Group.GroupType.Guid == familyGroupTypeGuid &&
                                                    m.Group.CampusId.HasValue &&
                                                    m.Group.CampusId.Value == campusId ) );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.Email:
                                    var tbEmailFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsEmailFilter" ) as RockTextBox;
                                    if ( tbEmailFilter != null && !string.IsNullOrWhiteSpace( tbEmailFilter.Text ) )
                                    {
                                        qry = qry.Where( r =>
                                            r.PersonAlias.Person.Email != null &&
                                            r.PersonAlias.Person.Email.Contains( tbEmailFilter.Text ) );
                                    }

                                    break;

                                case RegistrationPersonFieldType.Birthdate:
                                    var drpBirthdateFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "drpRegistrantsBirthdateFilter" ) as DateRangePicker;
                                    if ( drpBirthdateFilter != null )
                                    {
                                        if ( drpBirthdateFilter.LowerValue.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.BirthDate.HasValue &&
                                                r.PersonAlias.Person.BirthDate.Value >= drpBirthdateFilter.LowerValue.Value );
                                        }

                                        if ( drpBirthdateFilter.UpperValue.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.BirthDate.HasValue &&
                                                r.PersonAlias.Person.BirthDate.Value <= drpBirthdateFilter.UpperValue.Value );
                                        }
                                    }

                                    break;
                                case RegistrationPersonFieldType.MiddleName:
                                    var tbMiddleNameFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsMiddleNameFilter" ) as RockTextBox;
                                    if ( tbMiddleNameFilter != null && !string.IsNullOrWhiteSpace( tbMiddleNameFilter.Text ) )
                                    {
                                        qry = qry.Where( r =>
                                            r.PersonAlias.Person.MiddleName != null &&
                                            r.PersonAlias.Person.MiddleName.Contains( tbMiddleNameFilter.Text ) );
                                    }
                                    break;
                                case RegistrationPersonFieldType.AnniversaryDate:
                                    var drpAnniversaryDateFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "drpRegistrantsAnniversaryDateFilter" ) as DateRangePicker;
                                    if ( drpAnniversaryDateFilter != null )
                                    {
                                        if ( drpAnniversaryDateFilter.LowerValue.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.AnniversaryDate.HasValue &&
                                                r.PersonAlias.Person.AnniversaryDate.Value >= drpAnniversaryDateFilter.LowerValue.Value );
                                        }

                                        if ( drpAnniversaryDateFilter.UpperValue.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.AnniversaryDate.HasValue &&
                                                r.PersonAlias.Person.AnniversaryDate.Value <= drpAnniversaryDateFilter.UpperValue.Value );
                                        }
                                    }
                                    break;
                                case RegistrationPersonFieldType.Grade:
                                    var gpGradeFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "gpRegistrantsGradeFilter" ) as GradePicker;
                                    if ( gpGradeFilter != null )
                                    {
                                        int? graduationYear = Person.GraduationYearFromGradeOffset( gpGradeFilter.SelectedValueAsInt( false ) );
                                        if ( graduationYear.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.GraduationYear.HasValue &&
                                                r.PersonAlias.Person.GraduationYear == graduationYear.Value );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.Gender:
                                    var ddlGenderFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "ddlRegistrantsGenderFilter" ) as RockDropDownList;
                                    if ( ddlGenderFilter != null )
                                    {
                                        var gender = ddlGenderFilter.SelectedValue.ConvertToEnumOrNull<Gender>();
                                        if ( gender.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.Gender == gender );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.MaritalStatus:
                                    var dvpMaritalStatusFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "dvpRegistrantsMaritalStatusFilter" ) as DefinedValuePicker;
                                    if ( dvpMaritalStatusFilter != null )
                                    {
                                        var maritalStatusId = dvpMaritalStatusFilter.SelectedValue.AsIntegerOrNull();
                                        if ( maritalStatusId.HasValue )
                                        {
                                            qry = qry.Where( r =>
                                                r.PersonAlias.Person.MaritalStatusValueId.HasValue &&
                                                r.PersonAlias.Person.MaritalStatusValueId.Value == maritalStatusId.Value );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.MobilePhone:
                                    var tbMobilePhoneFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsMobilePhoneFilter" ) as RockTextBox;
                                    if ( tbMobilePhoneFilter != null && !string.IsNullOrWhiteSpace( tbMobilePhoneFilter.Text ) )
                                    {
                                        string numericPhone = tbMobilePhoneFilter.Text.AsNumeric();
                                        if ( !string.IsNullOrEmpty( numericPhone ) )
                                        {
                                            var phoneNumberPersonIdQry = new PhoneNumberService( rockContext )
                                                .Queryable()
                                                .Where( a => a.Number.Contains( numericPhone ) )
                                                .Select( a => a.PersonId );

                                            qry = qry.Where( r => phoneNumberPersonIdQry.Contains( r.PersonAlias.PersonId ) );
                                        }
                                    }

                                    break;

                                case RegistrationPersonFieldType.HomePhone:
                                    var tbRegistrantsHomePhoneFilter = phRegistrantsRegistrantFormFieldFilters.FindControl( "tbRegistrantsHomePhoneFilter" ) as RockTextBox;
                                    if ( tbRegistrantsHomePhoneFilter != null && !string.IsNullOrWhiteSpace( tbRegistrantsHomePhoneFilter.Text ) )
                                    {
                                        string numericPhone = tbRegistrantsHomePhoneFilter.Text.AsNumeric();
                                        if ( !string.IsNullOrEmpty( numericPhone ) )
                                        {
                                            var phoneNumberPersonIdQry = new PhoneNumberService( rockContext )
                                                .Queryable()
                                                .Where( a => a.Number.Contains( numericPhone ) )
                                                .Select( a => a.PersonId );

                                            qry = qry.Where( r => phoneNumberPersonIdQry.Contains( r.PersonAlias.PersonId ) );
                                        }
                                    }

                                    break;
                            }
                        }

                        // Get all the registrant attributes selected to be on grid
                        registrantAttributes = RegistrantFields
                            .Where( f =>
                                f.Attribute != null &&
                                f.FieldSource == RegistrationFieldSource.RegistrantAttribute )
                            .Select( f => f.Attribute )
                            .ToList();
                        registrantAttributeIds = registrantAttributes.Select( a => a.Id ).Distinct().ToList();

                        // Filter query by any configured registrant attribute filters
                        if ( registrantAttributes != null && registrantAttributes.Any() )
                        {
                            foreach ( var attribute in registrantAttributes )
                            {
                                var filterControl = phRegistrantsRegistrantFormFieldFilters.FindControl( "filterRegistrants_" + attribute.Id.ToString() );
                                qry = attribute.FieldType.Field.ApplyAttributeQueryFilter( qry, filterControl, attribute, registrationRegistrantService, Rock.Reporting.FilterMode.SimpleFilter );
                            }
                        }

                        // Get all the person attributes selected to be on grid
                        personAttributes = RegistrantFields
                            .Where( f =>
                                f.Attribute != null &&
                                f.FieldSource == RegistrationFieldSource.PersonAttribute )
                            .Select( f => f.Attribute )
                            .ToList();
                        personAttributesIds = personAttributes.Select( a => a.Id ).Distinct().ToList();

                        // Filter query by any configured person attribute filters
                        if ( personAttributes != null && personAttributes.Any() )
                        {
                            PersonService personService = new PersonService( rockContext );
                            var personQry = personService.Queryable().AsNoTracking();
                            foreach ( var attribute in personAttributes )
                            {
                                var filterControl = phRegistrantsRegistrantFormFieldFilters.FindControl( "filterRegistrants_" + attribute.Id.ToString() );
                                personQry = attribute.FieldType.Field.ApplyAttributeQueryFilter( personQry, filterControl, attribute, personService, Rock.Reporting.FilterMode.SimpleFilter );
                            }

                            qry = qry.Where( r => personQry.Any( p => p.Id == r.PersonAlias.PersonId ) );
                        }



                        // Get all the group member attributes selected to be on grid
                        groupMemberAttributes = RegistrantFields
                            .Where( f =>
                                f.Attribute != null &&
                                f.FieldSource == RegistrationFieldSource.GroupMemberAttribute )
                            .Select( f => f.Attribute )
                            .ToList();
                        groupMemberAttributesIds = groupMemberAttributes.Select( a => a.Id ).Distinct().ToList();

                        // Filter query by any configured person attribute filters
                        if ( groupMemberAttributes != null && groupMemberAttributes.Any() )
                        {
                            var groupMemberService = new GroupMemberService( rockContext );
                            var groupMemberQry = groupMemberService.Queryable().AsNoTracking();
                            foreach ( var attribute in groupMemberAttributes )
                            {
                                var filterControl = phRegistrantsRegistrantFormFieldFilters.FindControl( "filterRegistrants_" + attribute.Id.ToString() );
                                groupMemberQry = attribute.FieldType.Field.ApplyAttributeQueryFilter( groupMemberQry, filterControl, attribute, groupMemberService, Rock.Reporting.FilterMode.SimpleFilter );
                            }

                            qry = qry.Where( r => groupMemberQry.Any( g => g.Id == r.GroupMemberId ) );
                        }
                    }

                    // Sort the query
                    IOrderedQueryable<RegistrationRegistrant> orderedQry = null;
                    SortProperty sortProperty = gRegistrants.SortProperty;
                    if ( sortProperty != null )
                    {
                        orderedQry = qry.Sort( sortProperty );
                    }
                    else
                    {
                        orderedQry = qry
                            .OrderBy( r => r.PersonAlias.Person.LastName )
                            .ThenBy( r => r.PersonAlias.Person.NickName );
                    }

                    // increase the timeout just in case. A complex filter on the grid might slow things down
                    rockContext.Database.CommandTimeout = 180;

                    // Set the grids LinqDataSource which will run query and set results for current page
                    gRegistrants.SetLinqDataSource<RegistrationRegistrant>( orderedQry );

                    if ( RegistrantFields != null )
                    {
                        // Get the query results for the current page
                        var currentPageRegistrants = gRegistrants.DataSource as List<RegistrationRegistrant>;
                        if ( currentPageRegistrants != null )
                        {
                            // Get all the registrant ids in current page of query results
                            var registrantIds = currentPageRegistrants
                                .Select( r => r.Id )
                                .Distinct()
                                .ToList();

                            // Get all the person ids in current page of query results
                            var currentPagePersonIds = currentPageRegistrants
                                .Select( r => r.PersonAlias.PersonId )
                                .Distinct()
                                .ToList();

                            // Get all the group member ids and the group id in current page of query results
                            var groupMemberIds = new List<int>();
                            GroupLinks = new Dictionary<int, string>();
                            foreach ( var groupMember in currentPageRegistrants
                                .Where( m =>
                                    m.GroupMember != null &&
                                    m.GroupMember.Group != null )
                                .Select( m => m.GroupMember ) )
                            {
                                groupMemberIds.Add( groupMember.Id );

                                string linkedPageUrl = LinkedPageUrl( "GroupDetailPage", new Dictionary<string, string> { { "GroupId", groupMember.GroupId.ToString() } } );
                                GroupLinks.AddOrIgnore(
                                    groupMember.GroupId,
                                    isExporting ? groupMember.Group.Name : string.Format( "<a href='{0}'>{1}</a>", linkedPageUrl, groupMember.Group.Name ) );
                            }

                            // If the campus column was selected to be displayed on grid, preload all the people's
                            // campuses so that the databind does not need to query each row
                            if ( preloadCampusValues )
                            {
                                PersonCampusIds = new Dictionary<int, List<int>>();

                                Guid familyGroupTypeGuid = Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid();
                                foreach ( var personCampusList in new GroupMemberService( rockContext )
                                    .Queryable().AsNoTracking()
                                    .Where( m =>
                                        m.Group.GroupType.Guid == familyGroupTypeGuid &&
                                        currentPagePersonIds.Contains( m.PersonId ) )
                                    .GroupBy( m => m.PersonId )
                                    .Select( m => new
                                    {
                                        PersonId = m.Key,
                                        CampusIds = m
                                            .Where( g => g.Group.CampusId.HasValue )
                                            .Select( g => g.Group.CampusId.Value )
                                            .ToList()
                                    } ) )
                                {
                                    PersonCampusIds.Add( personCampusList.PersonId, personCampusList.CampusIds );
                                }
                            }

                            // If there are any attributes that were selected to be displayed, we're going
                            // to try and read all attribute values in one query and then put them into a
                            // custom grid ObjectList property so that the AttributeField columns don't need
                            // to do the LoadAttributes and querying of values for each row/column
                            if ( personAttributesIds.Any() || groupMemberAttributesIds.Any() || registrantAttributeIds.Any() )
                            {
                                // Query the attribute values for all rows and attributes
                                var attributeValues = new AttributeValueService( rockContext )
                                    .Queryable( "Attribute" ).AsNoTracking()
                                    .Where( v =>
                                        v.EntityId.HasValue &&
                                        (
                                            (
                                                personAttributesIds.Contains( v.AttributeId ) &&
                                                currentPagePersonIds.Contains( v.EntityId.Value )
                                            ) ||
                                            (
                                                groupMemberAttributesIds.Contains( v.AttributeId ) &&
                                                groupMemberIds.Contains( v.EntityId.Value )
                                            ) ||
                                            (
                                                registrantAttributeIds.Contains( v.AttributeId ) &&
                                                registrantIds.Contains( v.EntityId.Value )
                                            )
                                        ) ).ToList();

                                // Get the attributes to add to each row's object
                                var attributes = new Dictionary<string, AttributeCache>();
                                RegistrantFields
                                        .Where( f => f.Attribute != null )
                                        .Select( f => f.Attribute )
                                        .ToList()
                                    .ForEach( a => attributes
                                        .Add( a.Id.ToString() + a.Key, a ) );

                                // Initialize the grid's object list
                                gRegistrants.ObjectList = new Dictionary<string, object>();

                                // Loop through each of the current page's registrants and build an attribute
                                // field object for storing attributes and the values for each of the registrants
                                foreach ( var registrant in currentPageRegistrants )
                                {
                                    // Create a row attribute object
                                    var attributeFieldObject = new AttributeFieldObject();

                                    // Add the attributes to the attribute object
                                    attributeFieldObject.Attributes = attributes;

                                    // Add any person attribute values to object
                                    attributeValues
                                        .Where( v =>
                                            personAttributesIds.Contains( v.AttributeId ) &&
                                            v.EntityId.Value == registrant.PersonAlias.PersonId )
                                        .ToList()
                                        .ForEach( v => attributeFieldObject.AttributeValues
                                            .Add( v.AttributeId.ToString() + v.Attribute.Key, new AttributeValueCache( v ) ) );

                                    // Add any group member attribute values to object
                                    if ( registrant.GroupMemberId.HasValue )
                                    {
                                        attributeValues
                                            .Where( v =>
                                                groupMemberAttributesIds.Contains( v.AttributeId ) &&
                                                v.EntityId.Value == registrant.GroupMemberId.Value )
                                            .ToList()
                                            .ForEach( v => attributeFieldObject.AttributeValues
                                                .Add( v.AttributeId.ToString() + v.Attribute.Key, new AttributeValueCache( v ) ) );
                                    }

                                    // Add any registrant attribute values to object
                                    attributeValues
                                        .Where( v =>
                                            registrantAttributeIds.Contains( v.AttributeId ) &&
                                            v.EntityId.Value == registrant.Id )
                                        .ToList()
                                        .ForEach( v => attributeFieldObject.AttributeValues
                                            .Add( v.AttributeId.ToString() + v.Attribute.Key, new AttributeValueCache( v ) ) );

                                    // Add row attribute object to grid's object list
                                    gRegistrants.ObjectList.Add( registrant.Id.ToString(), attributeFieldObject );
                                }
                            }
                        }
                    }

                    gRegistrants.DataBind();
                }
            }
        }

        private void SetPhoneDictionary( RockContext rockContext, List<int> personIds )
        {
            if ( RegistrantFields.Any( f => f.PersonFieldType != null && f.PersonFieldType == RegistrationPersonFieldType.MobilePhone ) )
            {
                var phoneNumberService = new PhoneNumberService( rockContext );
                foreach ( var personId in personIds )
                {
                    _mobilePhoneNumbers[personId] = phoneNumberService.GetNumberByPersonIdAndType( personId, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE );
                }
            }

            if ( RegistrantFields.Any( f => f.PersonFieldType != null && f.PersonFieldType == RegistrationPersonFieldType.HomePhone ) )
            {
                var phoneNumberService = new PhoneNumberService( rockContext );
                foreach ( var personId in personIds )
                {
                    _homePhoneNumbers[personId] = phoneNumberService.GetNumberByPersonIdAndType( personId, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME );
                }
            }
        }

        /// <summary>
        /// Gets all of the form fields that were configured as 'Show on Grid' for the registration template
        /// </summary>
        /// <param name="registrationInstance">The registration instance.</param>
        private void LoadRegistrantFormFields( RegistrationInstance registrationInstance )
        {
            RegistrantFields = new List<RegistrantFormField>();

            if ( registrationInstance != null )
            {
                foreach ( var form in registrationInstance.RegistrationTemplate.Forms )
                {
                    foreach ( var formField in form.Fields
                        .Where( f => f.IsGridField )
                        .OrderBy( f => f.Order ) )
                    {
                        if ( formField.FieldSource == RegistrationFieldSource.PersonField )
                        {
                            if ( formField.PersonFieldType != RegistrationPersonFieldType.FirstName &&
                                formField.PersonFieldType != RegistrationPersonFieldType.LastName )
                            {
                                RegistrantFields.Add(
                                    new RegistrantFormField
                                    {
                                        FieldSource = formField.FieldSource,
                                        PersonFieldType = formField.PersonFieldType
                                    } );
                            }
                        }
                        else
                        {
                            RegistrantFields.Add(
                                new RegistrantFormField
                                {
                                    FieldSource = formField.FieldSource,
                                    Attribute = AttributeCache.Get( formField.AttributeId.Value )
                                } );
                        }
                    }
                }
            }
        }

        private void ClearGrid(Grid grid )
        {
            // Remove any of the dynamic person fields
            var dynamicColumns = new List<string> {
                "PersonAlias.Person.BirthDate",
            };
            foreach ( var column in grid.Columns
                .OfType<BoundField>()
                .Where( c => dynamicColumns.Contains( c.DataField ) )
                .ToList() )
            {
                grid.Columns.Remove( column );
            }

            // Remove any of the dynamic attribute fields
            foreach ( var column in grid.Columns
                .OfType<AttributeField>()
                .ToList() )
            {
                grid.Columns.Remove( column );
            }

            // Remove the fees field
            foreach ( var column in grid.Columns
                .OfType<TemplateField>()
                .Where( c => c.HeaderText == "Fees" )
                .ToList() )
            {
                grid.Columns.Remove( column );
            }

            // Remove the delete field
            foreach ( var column in grid.Columns
                .OfType<DeleteField>()
                .ToList() )
            {
                grid.Columns.Remove( column );
            }

            // Remove the delete field
            foreach ( var column in grid.Columns
                .OfType<GroupPickerField>()
                .ToList() )
            {
                grid.Columns.Remove( column );
            }
        }

        /// <summary>
        /// Adds the filter controls and grid columns for all of the registration template's form fields
        /// that were configured to 'Show on Grid'
        /// </summary>
        private void RegistrantsTabAddDynamicControls( bool setValues )
        {
            phRegistrantsRegistrantFormFieldFilters.Controls.Clear();
            //phGroupPlacementsFormFieldFilters.Controls.Clear();
            //phWaitListFormFieldFilters.Controls.Clear();

            //ClearGrid( gGroupPlacements );
            ClearGrid( gRegistrants );
            //ClearGrid( gWaitList );

            string dataFieldExpression = string.Empty;

            if ( RegistrantFields != null )
            {
                foreach ( var field in RegistrantFields )
                {
                    if ( field.FieldSource == RegistrationFieldSource.PersonField && field.PersonFieldType.HasValue )
                    {
                        switch ( field.PersonFieldType.Value )
                        {
                            case RegistrationPersonFieldType.Campus:
                                var ddlRegistrantsCampus = new RockDropDownList();
                                ddlRegistrantsCampus.ID = "ddlRegistrantsCampus";
                                ddlRegistrantsCampus.Label = "Home Campus";
                                ddlRegistrantsCampus.DataValueField = "Id";
                                ddlRegistrantsCampus.DataTextField = "Name";
                                ddlRegistrantsCampus.DataSource = CampusCache.All();
                                ddlRegistrantsCampus.DataBind();
                                ddlRegistrantsCampus.Items.Insert( 0, new ListItem( string.Empty, string.Empty ) );

                                if ( setValues )
                                {
                                    ddlRegistrantsCampus.SetValue( fRegistrants.GetUserPreference( "Home Campus" ) );
                                }

                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( ddlRegistrantsCampus );

                                var ddlGroupPlacementsCampus = new RockDropDownList();
                                ddlGroupPlacementsCampus.ID = "ddlGroupPlacementsCampus";
                                ddlGroupPlacementsCampus.Label = "Home Campus";
                                ddlGroupPlacementsCampus.DataValueField = "Id";
                                ddlGroupPlacementsCampus.DataTextField = "Name";
                                ddlGroupPlacementsCampus.DataSource = CampusCache.All();
                                ddlGroupPlacementsCampus.DataBind();
                                ddlGroupPlacementsCampus.Items.Insert( 0, new ListItem( string.Empty, string.Empty ) );

                                var templateField = new RockLiteralField();
                                templateField.ID = "lRegistrantsCampus";
                                templateField.HeaderText = "Campus";
                                gRegistrants.Columns.Add( templateField );

                                break;

                            case RegistrationPersonFieldType.Email:
                                var tbRegistrantsEmailFilter = new RockTextBox();
                                tbRegistrantsEmailFilter.ID = "tbRegistrantsEmailFilter";
                                tbRegistrantsEmailFilter.Label = "Email";

                                if ( setValues )
                                {
                                    tbRegistrantsEmailFilter.Text = fRegistrants.GetUserPreference( "Email" );
                                }

                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( tbRegistrantsEmailFilter );

                                dataFieldExpression = "PersonAlias.Person.Email";
                                var emailField = new RockBoundField();
                                emailField.DataField = dataFieldExpression;
                                emailField.HeaderText = "Email";
                                emailField.SortExpression = dataFieldExpression;
                                gRegistrants.Columns.Add( emailField );

                                break;

                            case RegistrationPersonFieldType.Birthdate:
                                var drpRegistrantsBirthdateFilter = new DateRangePicker();
                                drpRegistrantsBirthdateFilter.ID = "drpRegistrantsBirthdateFilter";
                                drpRegistrantsBirthdateFilter.Label = "Birthdate Range";

                                if ( setValues )
                                {
                                    drpRegistrantsBirthdateFilter.DelimitedValues = fRegistrants.GetUserPreference( "Birthdate Range" );
                                }

                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( drpRegistrantsBirthdateFilter );

                                var drpGroupPlacementsBirthdateFilter = new DateRangePicker();
                                drpGroupPlacementsBirthdateFilter.ID = "drpGroupPlacementsBirthdateFilter";
                                drpGroupPlacementsBirthdateFilter.Label = "Birthdate Range";

                                dataFieldExpression = "PersonAlias.Person.BirthDate";
                                var birthdateField = new DateField();
                                birthdateField.DataField = dataFieldExpression;
                                birthdateField.HeaderText = "Birthdate";
                                birthdateField.IncludeAge = true;
                                birthdateField.SortExpression = dataFieldExpression;
                                gRegistrants.Columns.Add( birthdateField );

                                break;
                            case RegistrationPersonFieldType.MiddleName:
                                var tbRegistrantsMiddleNameFilter = new RockTextBox();
                                tbRegistrantsMiddleNameFilter.ID = "tbRegistrantsMiddleNameFilter";
                                tbRegistrantsMiddleNameFilter.Label = "MiddleName";

                                if ( setValues )
                                {
                                    tbRegistrantsMiddleNameFilter.Text = fRegistrants.GetUserPreference( "MiddleName" );
                                }

                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( tbRegistrantsMiddleNameFilter );

                                var tbGroupPlacementsMiddleNameFilter = new RockTextBox();
                                tbGroupPlacementsMiddleNameFilter.ID = "tbGroupPlacementsMiddleNameFilter";
                                tbGroupPlacementsMiddleNameFilter.Label = "MiddleName";

                                dataFieldExpression = "PersonAlias.Person.MiddleName";
                                var middleNameField = new RockBoundField();
                                middleNameField.DataField = dataFieldExpression;
                                middleNameField.HeaderText = "MiddleName";
                                middleNameField.SortExpression = dataFieldExpression;
                                gRegistrants.Columns.Add( middleNameField );

                                break;

                            case RegistrationPersonFieldType.AnniversaryDate:
                                var drpRegistrantsAnniversaryDateFilter = new DateRangePicker();
                                drpRegistrantsAnniversaryDateFilter.ID = "drpRegistrantsAnniversaryDateFilter";
                                drpRegistrantsAnniversaryDateFilter.Label = "AnniversaryDate Range";

                                if ( setValues )
                                {
                                    drpRegistrantsAnniversaryDateFilter.DelimitedValues = fRegistrants.GetUserPreference( "AnniversaryDate Range" );
                                }

                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( drpRegistrantsAnniversaryDateFilter );

                                var drpGroupPlacementsAnniversaryDateFilter = new DateRangePicker();
                                drpGroupPlacementsAnniversaryDateFilter.ID = "drpGroupPlacementsAnniversaryDateFilter";
                                drpGroupPlacementsAnniversaryDateFilter.Label = "AnniversaryDate Range";

                                dataFieldExpression = "PersonAlias.Person.AnniversaryDate";
                                var anniversaryDateField = new DateField();
                                anniversaryDateField.DataField = dataFieldExpression;
                                anniversaryDateField.HeaderText = "Anniversary Date";
                                anniversaryDateField.IncludeAge = true;
                                anniversaryDateField.SortExpression = dataFieldExpression;
                                gRegistrants.Columns.Add( anniversaryDateField );

                                break;
                            case RegistrationPersonFieldType.Grade:
                                var gpRegistrantsGradeFilter = new GradePicker();
                                gpRegistrantsGradeFilter.ID = "gpRegistrantsGradeFilter";
                                gpRegistrantsGradeFilter.Label = "Grade";
                                gpRegistrantsGradeFilter.UseAbbreviation = true;
                                gpRegistrantsGradeFilter.UseGradeOffsetAsValue = true;
                                gpRegistrantsGradeFilter.CssClass = "input-width-md";
                                    
                                // Since 12th grade is the 0 Value, we need to handle the "no user preference" differently
                                // by not calling SetValue otherwise it will select 12th grade.
                                if ( setValues )
                                {
                                    var registrantsGradeUserPreference = fRegistrants.GetUserPreference( "Grade" ).AsIntegerOrNull();
                                    if ( registrantsGradeUserPreference != null )
                                    {
                                        gpRegistrantsGradeFilter.SetValue( registrantsGradeUserPreference );
                                    }
                                }

                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( gpRegistrantsGradeFilter );

                                var gpGroupPlacementsGradeFilter = new GradePicker();
                                gpGroupPlacementsGradeFilter.ID = "gpGroupPlacementsGradeFilter";
                                gpGroupPlacementsGradeFilter.Label = "Grade";
                                gpGroupPlacementsGradeFilter.UseAbbreviation = true;
                                gpGroupPlacementsGradeFilter.UseGradeOffsetAsValue = true;
                                gpGroupPlacementsGradeFilter.CssClass = "input-width-md";

                                // 2017-01-13 as discussed, changing this to Grade but keeping the sort based on grad year
                                dataFieldExpression = "PersonAlias.Person.GradeFormatted";
                                var gradeField = new RockBoundField();
                                gradeField.DataField = dataFieldExpression;
                                gradeField.HeaderText = "Grade";
                                gradeField.SortExpression = "PersonAlias.Person.GraduationYear";
                                gRegistrants.Columns.Add( gradeField );

                                break;

                            case RegistrationPersonFieldType.Gender:
                                var ddlRegistrantsGenderFilter = new RockDropDownList();
                                ddlRegistrantsGenderFilter.BindToEnum<Gender>( true );
                                ddlRegistrantsGenderFilter.ID = "ddlRegistrantsGenderFilter";
                                ddlRegistrantsGenderFilter.Label = "Gender";

                                if ( setValues )
                                {
                                    ddlRegistrantsGenderFilter.SetValue( fRegistrants.GetUserPreference( "Gender" ) );
                                }

                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( ddlRegistrantsGenderFilter );

                                dataFieldExpression = "PersonAlias.Person.Gender";
                                var genderField = new EnumField();
                                genderField.DataField = dataFieldExpression;
                                genderField.HeaderText = "Gender";
                                genderField.SortExpression = dataFieldExpression;
                                gRegistrants.Columns.Add( genderField );

                                break;

                            case RegistrationPersonFieldType.MaritalStatus:
                                var dvpRegistrantsMaritalStatusFilter = new DefinedValuePicker();
                                dvpRegistrantsMaritalStatusFilter.ID = "dvpRegistrantsMaritalStatusFilter";
                                dvpRegistrantsMaritalStatusFilter.DefinedTypeId = DefinedTypeCache.Get( Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS.AsGuid() ).Id;
                                dvpRegistrantsMaritalStatusFilter.Label = "Marital Status";

                                if ( setValues )
                                {
                                    dvpRegistrantsMaritalStatusFilter.SetValue( fRegistrants.GetUserPreference( "Marital Status" ) );
                                }

                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( dvpRegistrantsMaritalStatusFilter );

                                var dvpGroupPlacementsMaritalStatusFilter = new DefinedValuePicker();
                                dvpGroupPlacementsMaritalStatusFilter.ID = "dvpGroupPlacementsMaritalStatusFilter";
                                dvpGroupPlacementsMaritalStatusFilter.DefinedTypeId = DefinedTypeCache.Get( Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS.AsGuid() ).Id;
                                dvpGroupPlacementsMaritalStatusFilter.Label = "Marital Status";

                                dataFieldExpression = "PersonAlias.Person.MaritalStatusValue.Value";
                                var maritalStatusField = new RockBoundField();
                                maritalStatusField.DataField = dataFieldExpression;
                                maritalStatusField.HeaderText = "MaritalStatus";
                                maritalStatusField.SortExpression = dataFieldExpression;
                                gRegistrants.Columns.Add( maritalStatusField );

                                break;

                            case RegistrationPersonFieldType.MobilePhone:
                                // Per discussion this should not have "Phone" appended to the end if it's missing.
                                var mobileLabel = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE ).Value;

                                var tbRegistrantsMobilePhoneFilter = new RockTextBox();
                                tbRegistrantsMobilePhoneFilter.ID = "tbRegistrantsMobilePhoneFilter";
                                tbRegistrantsMobilePhoneFilter.Label = mobileLabel;

                                if ( setValues )
                                {
                                    tbRegistrantsMobilePhoneFilter.Text = fRegistrants.GetUserPreference( "Cell Phone" );
                                }

                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( tbRegistrantsMobilePhoneFilter );

                                var phoneNumbersField = new RockLiteralField();
                                phoneNumbersField.ID = "lRegistrantsMobile";
                                phoneNumbersField.HeaderText = mobileLabel;
                                gRegistrants.Columns.Add( phoneNumbersField );

                                break;

                            case RegistrationPersonFieldType.HomePhone:
                                // Per discussion this should not have "Phone" appended to the end if it's missing.
                                var homePhoneLabel = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME ).Value;

                                var tbRegistrantsHomePhoneFilter = new RockTextBox();
                                tbRegistrantsHomePhoneFilter.ID = "tbRegistrantsHomePhoneFilter";
                                tbRegistrantsHomePhoneFilter.Label = homePhoneLabel;

                                if ( setValues )
                                {
                                    tbRegistrantsHomePhoneFilter.Text = fRegistrants.GetUserPreference( "Home Phone" );
                                }

                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( tbRegistrantsHomePhoneFilter );

                                var homePhoneNumbersField = new RockLiteralField();
                                homePhoneNumbersField.ID = "lRegistrantsHomePhone";
                                homePhoneNumbersField.HeaderText = homePhoneLabel;
                                gRegistrants.Columns.Add( homePhoneNumbersField );

                                break;

                            case RegistrationPersonFieldType.Address:
                                var addressField = new RockLiteralField();
                                addressField.ID = "lRegistrantsAddress";
                                addressField.HeaderText = "Address";
                                // There are specific Street1, Street2, City, etc. fields included instead
                                addressField.ExcelExportBehavior = ExcelExportBehavior.NeverInclude;
                                gRegistrants.Columns.Add( addressField );

                                break;
                        }
                    }
                    else if ( field.Attribute != null )
                    {
                        var attribute = field.Attribute;

                        // add dynamic filter to registrant grid
                        var registrantsControl = attribute.FieldType.Field.FilterControl( attribute.QualifierValues, "filterRegistrants_" + attribute.Id.ToString(), false, Rock.Reporting.FilterMode.SimpleFilter );
                        if ( registrantsControl != null )
                        {
                            if ( registrantsControl is IRockControl )
                            {
                                var rockControl = (IRockControl) registrantsControl;
                                rockControl.Label = attribute.Name;
                                rockControl.Help = attribute.Description;
                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( registrantsControl );
                            }
                            else
                            {
                                var wrapper = new RockControlWrapper();
                                wrapper.ID = registrantsControl.ID + "_wrapper";
                                wrapper.Label = attribute.Name;
                                wrapper.Controls.Add( registrantsControl );
                                phRegistrantsRegistrantFormFieldFilters.Controls.Add( wrapper );
                            }

                            if ( setValues )
                            {
                                string savedValue = fRegistrants.GetUserPreference( attribute.Key );
                                if ( !string.IsNullOrWhiteSpace( savedValue ) )
                                {
                                    try
                                    {
                                        var values = JsonConvert.DeserializeObject<List<string>>( savedValue );
                                        attribute.FieldType.Field.SetFilterValues( registrantsControl, attribute.QualifierValues, values );
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                        }

                        // add dynamic filter to registrant grid
                        var groupPlacementsControl = attribute.FieldType.Field.FilterControl( attribute.QualifierValues, "filterGroupPlacements_" + attribute.Id.ToString(), false, Rock.Reporting.FilterMode.SimpleFilter );
                        if ( groupPlacementsControl != null )
                        {
                            if ( setValues )
                            {
                                string savedValue = fRegistrants.GetUserPreference( "GroupPlacements-" + attribute.Key );
                                if ( !string.IsNullOrWhiteSpace( savedValue ) )
                                {
                                    try
                                    {
                                        var values = JsonConvert.DeserializeObject<List<string>>( savedValue );
                                        attribute.FieldType.Field.SetFilterValues( groupPlacementsControl, attribute.QualifierValues, values );
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                        }

                        dataFieldExpression = attribute.Id.ToString() + attribute.Key;
                        bool columnExists = gRegistrants.Columns.OfType<AttributeField>().FirstOrDefault( a => a.DataField.Equals( dataFieldExpression ) ) != null;
                        if ( !columnExists )
                        {
                            AttributeField boundField = new AttributeField();
                            boundField.DataField = dataFieldExpression;
                            boundField.AttributeId = attribute.Id;
                            boundField.HeaderText = attribute.Name;

                            AttributeField boundField2 = new AttributeField();
                            boundField2.DataField = dataFieldExpression;
                            boundField2.AttributeId = attribute.Id;
                            boundField2.HeaderText = attribute.Name;

                            AttributeField boundField3 = new AttributeField();
                            boundField3.DataField = dataFieldExpression;
                            boundField3.AttributeId = attribute.Id;
                            boundField3.HeaderText = attribute.Name;

                            var attributeCache = Rock.Web.Cache.AttributeCache.Get( attribute.Id );
                            if ( attributeCache != null )
                            {
                                boundField.ItemStyle.HorizontalAlign = attributeCache.FieldType.Field.AlignValue;
                                boundField2.ItemStyle.HorizontalAlign = attributeCache.FieldType.Field.AlignValue;
                                boundField3.ItemStyle.HorizontalAlign = attributeCache.FieldType.Field.AlignValue;
                            }

                            gRegistrants.Columns.Add( boundField );
                        }
                    }
                }
            }

            // Add fee column
            var feeField = new RockLiteralField();
            feeField.ID = "lFees";
            feeField.HeaderText = "Fees";
            gRegistrants.Columns.Add( feeField );

            var deleteField = new DeleteField();
            gRegistrants.Columns.Add( deleteField );
            deleteField.Click += gRegistrants_Delete;
        }

        #endregion

        #endregion

        #region Helper Classes

        /// <summary>
        /// Helper class for tracking registration form fields
        /// </summary>
        [Serializable]
        public class RegistrantFormField
        {
            /// <summary>
            /// Gets or sets the field source.
            /// </summary>
            /// <value>
            /// The field source.
            /// </value>
            public RegistrationFieldSource FieldSource { get; set; }

            /// <summary>
            /// Gets or sets the type of the person field.
            /// </summary>
            /// <value>
            /// The type of the person field.
            /// </value>
            public RegistrationPersonFieldType? PersonFieldType { get; set; }

            /// <summary>
            /// Gets or sets the attribute.
            /// </summary>
            /// <value>
            /// The attribute.
            /// </value>
            public AttributeCache Attribute { get; set; }
        }

        #endregion
    }

}