// <copyright>
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
using Rock.Data;
using Rock.Model;
using Rock.Web;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Blocks.Event
{
    /// <summary>
    /// A Block that displays the discounts related to an event registration instance.
    /// </summary>
    [DisplayName( "Registration Instance - Discount List" )]
    [Category( "Event" )]
    [Description( "Displays the discounts related to an event registration instance." )]

    public partial class RegistrationInstanceDiscountList : Rock.Web.UI.RockBlock
    {
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

            RegistrationTemplateId = ViewState["RegistrationTemplateId"] as int? ?? 0;

            // don't set the values if this is a postback from a grid 'ClearFilter'
            bool setValues = this.Request.Params["__EVENTTARGET"] == null || !this.Request.Params["__EVENTTARGET"].EndsWith( "_lbClearFilter" );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            fDiscounts.ApplyFilterClick += fDiscounts_ApplyFilterClick;
            gDiscounts.GridRebind += gDiscounts_GridRebind;

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

        protected void fDiscounts_DisplayFilterValue( object sender, GridFilter.DisplayFilterValueArgs e )
        {
            switch ( e.Key )
            {
                case "DiscountDateRange":
                    e.Value = SlidingDateRangePicker.FormatDelimitedValues( e.Value );
                    break;

                case "DiscountCode":
                    // If that discount code is not in the list, don't show it anymore.
                    if ( ddlDiscountCode.Items.FindByText( e.Value ) == null )
                    {
                        e.Value = string.Empty;
                    }
                    break;

                case "DiscountCodeSearch":
                    break;

                default:
                    e.Value = string.Empty;
                    break;
            }
        }

        protected void fDiscounts_ClearFilterClick( object sender, EventArgs e )
        {
            fDiscounts.DeleteUserPreferences();
            tbDiscountCodeSearch.Enabled = true;
            BindDiscountsFilter();
        }

        protected void fDiscounts_ApplyFilterClick( object sender, EventArgs e )
        {
            fDiscounts.SaveUserPreference( "DiscountDateRange", "Discount Date Range", sdrpDiscountDateRange.DelimitedValues );
            fDiscounts.SaveUserPreference( "DiscountCode", "Discount Code", ddlDiscountCode.SelectedItem.Text );
            fDiscounts.SaveUserPreference( "DiscountCodeSearch", "Discount Code Search", tbDiscountCodeSearch.Text );

            BindDiscountsGrid();
        }

        protected void ddlDiscountCode_SelectedIndexChanged( object sender, EventArgs e )
        {
            tbDiscountCodeSearch.Enabled = ddlDiscountCode.SelectedIndex == 0 ? true : false;
        }

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
                }

                if ( registrationInstance.RegistrationTemplate == null && registrationInstance.RegistrationTemplateId > 0 )
                {
                    registrationInstance.RegistrationTemplate = new RegistrationTemplateService( rockContext )
                        .Get( registrationInstance.RegistrationTemplateId );
                }

                pnlDetails.Visible = true;
                hfRegistrationInstanceId.Value = registrationInstance.Id.ToString();
                hfRegistrationTemplateId.Value = registrationInstance.RegistrationTemplateId.ToString();
                RegistrationTemplateId = registrationInstance.RegistrationTemplateId;

                BindDiscountsFilter();
                BindDiscountsGrid();
            }
        }

        #endregion

        private void BindDiscountsFilter()
        {
            sdrpDiscountDateRange.DelimitedValues = fDiscounts.GetUserPreference( "DiscountDateRange" );
            Populate_ddlDiscountCode();
            ddlDiscountCode.SelectedIndex = ddlDiscountCode.Items.IndexOf( ddlDiscountCode.Items.FindByText( fDiscounts.GetUserPreference( "DiscountCode" ) ) );
            tbDiscountCodeSearch.Text = fDiscounts.GetUserPreference( "DiscountCodeSearch" );
        }

        private void BindDiscountsGrid()
        {
            int? instanceId = hfRegistrationInstanceId.Value.AsIntegerOrNull();
            if ( instanceId == null || instanceId == 0 )
            {
                return;
            }

            RegistrationTemplateDiscountService registrationTemplateDiscountService = new RegistrationTemplateDiscountService( new RockContext() );
            var data = registrationTemplateDiscountService.GetRegistrationInstanceDiscountCodeReport( ( int ) instanceId );

            // Add Date Range
            var dateRange = SlidingDateRangePicker.CalculateDateRangeFromDelimitedValues( sdrpDiscountDateRange.DelimitedValues );
            if ( dateRange.Start.HasValue )
            {
                data = data.Where( r => r.RegistrationDate >= dateRange.Start.Value );
            }

            if ( dateRange.End.HasValue )
            {
                data = data.Where( r => r.RegistrationDate < dateRange.End.Value );
            }

            // Discount code, use ddl if one is selected, otherwise try the search box.
            if ( ddlDiscountCode.SelectedIndex > 0 )
            {
                data = data.Where( r => r.DiscountCode == ddlDiscountCode.SelectedItem.Text );
            }
            else if ( tbDiscountCodeSearch.Text.IsNotNullOrWhiteSpace() )
            {
                System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex( tbDiscountCodeSearch.Text.ToLower() );
                data = data.Where( r => regex.IsMatch( r.DiscountCode.ToLower() ) );
            }

            var results = data.ToList();

            SortProperty sortProperty = gDiscounts.SortProperty;
            if ( sortProperty != null )
            {
                results = results.AsQueryable().Sort( sortProperty ).ToList();
            }
            else
            {
                results = results.OrderByDescending( d => d.RegistrationDate ).ToList();
            }


            gDiscounts.DataSource = results;
            gDiscounts.DataBind();

            PopulateTotals( results );
        }

        protected void gDiscounts_GridRebind( object sender, GridRebindEventArgs e )
        {
            if ( _RegistrationInstance == null )
            {
                return;
            }

            gDiscounts.ExportTitleName = _RegistrationInstance.Name + " - Discount Codes";
            gDiscounts.ExportFilename = gDiscounts.ExportFilename ?? _RegistrationInstance.Name + "DiscountCodes";
            BindDiscountsGrid();
        }

        private void PopulateTotals( List<TemplateDiscountReport> report )
        {
            lTotalTotalCost.Text = string.Format( GlobalAttributesCache.Value( "CurrencySymbol" ) + "{0:#,##0.00}", report.Sum( r => r.TotalCost ) );
            lTotalDiscountQualifiedCost.Text = string.Format( GlobalAttributesCache.Value( "CurrencySymbol" ) + "{0:#,##0.00}", report.Sum( r => r.DiscountQualifiedCost ) );
            lTotalDiscounts.Text = string.Format( GlobalAttributesCache.Value( "CurrencySymbol" ) + "{0:#,##0.00}", report.Sum( r => r.TotalDiscount ) );
            lTotalRegistrationCost.Text = string.Format( GlobalAttributesCache.Value( "CurrencySymbol" ) + "{0:#,##0.00}", report.Sum( r => r.RegistrationCost ) );
            lTotalRegistrations.Text = report.Count().ToString();
            lTotalRegistrants.Text = report.Sum( r => r.RegistrantCount ).ToString();
        }

        protected void Populate_ddlDiscountCode()
        {
            int? instanceId = hfRegistrationInstanceId.Value.AsIntegerOrNull();
            if ( instanceId == null || instanceId == 0 )
            {
                return;
            }

            var discountService = new RegistrationTemplateDiscountService( new RockContext() );
            var discountCodes = discountService.GetDiscountsForRegistrationInstance( instanceId ).AsNoTracking().OrderBy( d => d.Code ).ToList();

            ddlDiscountCode.Items.Clear();
            ddlDiscountCode.Items.Add( new ListItem() );
            foreach ( var discountCode in discountCodes )
            {
                ddlDiscountCode.Items.Add( new ListItem( discountCode.Code, discountCode.Id.ToString() ) );
            }
        }

        #endregion
    }
}