using System;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using iBudget;
using Microsoft.Xrm.Client;
using System.Collections.Generic;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace AnnualCostPlan.Pre.Plugin
{
    public class AnnualCostPlan_Pre : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            Microsoft.Xrm.Sdk.IPluginExecutionContext context = (Microsoft.Xrm.Sdk.IPluginExecutionContext)
                serviceProvider.GetService(typeof(Microsoft.Xrm.Sdk.IPluginExecutionContext));
            try
            {
                #region when trying to activate/ deactivate throw error

                if (context.MessageName == "SetStateDynamicEntity")
                {
                    if (context.Depth == 1)
                        throw new InvalidPluginExecutionException("Sorry but this operation cannot be performed manually.");
                }

                #endregion

                #region when creating/ updating a cost plan plan

                if ((context.MessageName == "Create" || context.MessageName == "Update")
                    && ((IPluginExecutionContext)(context.ParentContext)).MessageName != "Assign"
                    && context.Depth == 1)
                {
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService service = serviceFactory.CreateOrganizationService(null);

                    //Original Entity
                    Entity original_entity = new Entity();
                    original_entity = (Entity)context.InputParameters["Target"];

                    //Primary Entity Guid
                    Guid _annualcostplanid = new Guid();
                    if (context.MessageName == "Update")
                        _annualcostplanid = (Guid)original_entity.Id;

                    //create instance of iBudget
                    var iBudget = new XrmServiceContext(service);

                    #region required fields validation

                    string _errormsg = ""; int count = 0;
                    if (!original_entity.Attributes.Contains("apd_consumerid") || original_entity.Attributes["apd_consumerid"] == null)
                    {
                        _errormsg = _errormsg + "Consumer, ";
                        count += 1;
                    }
                    if (!original_entity.Attributes.Contains("apd_fiscalyearid") || original_entity.Attributes["apd_fiscalyearid"] == null)
                    {
                        _errormsg = _errormsg + "Fiscal Year, ";
                        count += 1;
                    }
                    if (!original_entity.Attributes.Contains("apd_ibg_iscopiedplan") || original_entity.Attributes["apd_ibg_iscopiedplan"] == null)
                    {
                        _errormsg = _errormsg + "Is Copied Plan, ";
                        count += 1;
                    }
                    if (!original_entity.Attributes.Contains("apd_ibg_iscopybuttonclicked") || original_entity.Attributes["apd_ibg_iscopybuttonclicked"] == null)
                    {
                        _errormsg = _errormsg + "Is Copy Button Clicked, ";
                        count += 1;
                    }

                    if (count == 1)
                    {
                        _errormsg = _errormsg.TrimEnd(new char[] { ',', ' ' });
                        throw new InvalidPluginExecutionException(_errormsg + " is a required field");
                    }
                    else if (count > 1)
                    {
                        _errormsg = _errormsg.TrimEnd(new char[] { ',', ' ' });
                        throw new InvalidPluginExecutionException(_errormsg + " are required fields");
                    }

                    #endregion

                    bool _isitacopiedrecord = false;
                    bool _iscopybuttonclicked = false;

                    _isitacopiedrecord = (bool)original_entity.Attributes["apd_ibg_iscopiedplan"];
                    _iscopybuttonclicked = (bool)original_entity.Attributes["apd_ibg_iscopybuttonclicked"];


                    #region retrieve consumer details

                    var consumer = (from c in iBudget.APD_consumerSet
                                    where c.APD_consumerId == ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id
                                    select new { c.APD_name, c.apd_waiversupportcoordinatorid, c.APD_TierCode, c.APD_DateOfBirth }).FirstOrDefault();

                    if (consumer == null)
                        throw new InvalidPluginExecutionException("Details for associated Consumer not found.");

                    if (consumer.apd_waiversupportcoordinatorid == null)
                        throw new InvalidPluginExecutionException("Waiver Support Coordinator not assigned to Consumer.");

                    //if (context.MessageName == "Create")
                    //    original_entity["ownerid"] = new EntityReference(SystemUser.EntityLogicalName, consumer.apd_waiversupportcoordinatorid.Id);
                    #endregion


                    if (context.MessageName == "Create" && _isitacopiedrecord == false)
                    {
                        #region Annual Budget Fetch & Details

                        var annual_budget = (from ab in iBudget.APD_IBG_annualbudgetSet
                                             where ab.apd_consumerid.Id == ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id
                                             && ab.apd_fiscalyearid.Id == ((EntityReference)original_entity.Attributes["apd_fiscalyearid"]).Id
                                             select ab).ToList();

                        if (annual_budget.Count() == 0)
                            throw new InvalidPluginExecutionException("Annual budget does not exist for selected consumer for selected fiscal year.");
                        else
                        {
                            APD_IBG_annualbudget ab = annual_budget.FirstOrDefault();

                            original_entity["apd_ibg_annualbudgetid"] = new EntityReference(APD_IBG_annualbudget.EntityLogicalName, ab.APD_IBG_annualbudgetId.Value);

                            original_entity["apd_ibg_yearlybudget"] = new Money(ab.APD_ibg_YearlyBudget.HasValue ? ab.APD_ibg_YearlyBudget.Value : 0.0M);
                            original_entity["apd_ibg_flexiblespendingamount"] = new Money(ab.APD_IBG_FlexibleSpendingAmount.HasValue ? ab.APD_IBG_FlexibleSpendingAmount.Value : 0.0M);
                            original_entity["apd_ibg_emergencyreserveamount"] = new Money(ab.APD_IBG_EmergencyReserveAmount.HasValue ? ab.APD_IBG_EmergencyReserveAmount.Value : 0.0M);
                            original_entity["apd_ibg_supplementalfundamount"] = new Money(0.0M);
                            original_entity["apd_ibg_allocatedamount"] = new Money(ab.APD_IBG_AllocatedAmount.HasValue ? ab.APD_IBG_AllocatedAmount.Value : 0.0M);

                            original_entity["apd_ibg_budgetedamount"] = new Money(0.0M);
                            original_entity["apd_ibg_budgetedflexiblespendingamount"] = new Money(0.0M);
                            original_entity["apd_ibg_budgetedemergencyreserveamount"] = new Money(0.0M);
                            original_entity["apd_ibg_budgetedsupplementalfundamount"] = new Money(0.0M);
                            original_entity["apd_ibg_totalbudgetedamount"] = new Money(0.0M);

                            original_entity["apd_ibg_balanceamount"] = new Money(ab.APD_ibg_YearlyBudget.HasValue ? ab.APD_ibg_YearlyBudget.Value : 0.0M);
                            original_entity["apd_ibg_balanceflexiblespendingamount"] = new Money(ab.APD_IBG_FlexibleSpendingAmount.HasValue ? ab.APD_IBG_FlexibleSpendingAmount.Value : 0.0M);
                            original_entity["apd_ibg_balanceemergencyreserveamount"] = new Money(ab.APD_IBG_EmergencyReserveAmount.HasValue ? ab.APD_IBG_EmergencyReserveAmount.Value : 0.0M);
                            original_entity["apd_ibg_balancesupplementalfundamount"] = new Money(0.0M);
                            original_entity["apd_ibg_remainingbalanceamount"] = new Money((ab.APD_ibg_YearlyBudget.HasValue ? ab.APD_ibg_YearlyBudget.Value : 0.0M) +
                                                                                (ab.APD_IBG_FlexibleSpendingAmount.HasValue ? ab.APD_IBG_FlexibleSpendingAmount.Value : 0.0M) +
                                                                                (ab.APD_IBG_EmergencyReserveAmount.HasValue ? ab.APD_IBG_EmergencyReserveAmount.Value : 0.0M));
                        }

                        #endregion

                        #region retrieve fiscal year details

                        var fiscalyear = (from fy in iBudget.APD_fiscalyearSet
                                          where fy.APD_fiscalyearId == ((EntityReference)original_entity.Attributes["apd_fiscalyearid"]).Id
                                          select new { fy.APD_BeginDate, fy.APD_EndDate, fy.APD_fiscalyearname }).First();

                        original_entity["apd_ibg_annualcostplanname"] = "Cost Plan for " + consumer.APD_name + " For " + fiscalyear.APD_fiscalyearname;

                        #endregion

                        #region check for duplicate record

                        if (_isitacopiedrecord == false)
                        {
                            var acps = (from c in iBudget.APD_IBG_annualcostplanSet
                                        where c.apd_consumerid.Id == ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id
                                        && c.apd_fiscalyearid.Id == ((EntityReference)original_entity.Attributes["apd_fiscalyearid"]).Id
                                        && c.apd_ibg_annualbudgetid.Id == annual_budget.FirstOrDefault().APD_IBG_annualbudgetId.Value
                                        select new { c.statuscode }).ToList();

                            if (acps.Count() > 0)
                                throw new InvalidPluginExecutionException("Record cannot be created. Annual cost plan already exists for selected parameters: consumer, fiscal year, annual budget.");
                        }

                        #endregion

                        original_entity["apd_ibg_tiercode"] = consumer.APD_TierCode;
                    }

                    if (context.MessageName == "Update" && !_iscopybuttonclicked)
                    {
                        int _statuscode = 0;
                        int _processingstatus = 0;

                        int _prestatuscode = 0;
                        int _preprocessingstatus = 0;

                        #region required fields validation

                        _errormsg = ""; count = 0;
                        if (!original_entity.Attributes.Contains("statuscode") || original_entity.Attributes["statuscode"] == null)
                        {
                            _errormsg = _errormsg + "Cost Plan Status, ";
                            count += 1;
                        }
                        if (!original_entity.Attributes.Contains("apd_ibg_processingstatuscode") || original_entity.Attributes["apd_ibg_processingstatuscode"] == null)
                        {
                            _errormsg = _errormsg + "Processing Status, ";
                            count += 1;
                        }

                        if (count == 1)
                        {
                            _errormsg = _errormsg.TrimEnd(new char[] { ',', ' ' });
                            throw new InvalidPluginExecutionException(_errormsg + " is a required field");
                        }
                        else if (count > 1)
                        {
                            _errormsg = _errormsg.TrimEnd(new char[] { ',', ' ' });
                            throw new InvalidPluginExecutionException(_errormsg + " are required fields");
                        }

                        #endregion

                        #region _processrecord

                        _statuscode = ((OptionSetValue)(original_entity.Attributes["statuscode"])).Value;
                        _processingstatus = ((OptionSetValue)(original_entity.Attributes["apd_ibg_processingstatuscode"])).Value;

                        bool _isareareviewrequired = false;
                        bool _isareareviewrequired_temp = false;
                        bool _iscentralreviewrequired = false;
                        bool _iscentralreviewrequired_temp = false;
                        bool _processrecord = true;

                        if (_statuscode == (int)apd_ibg_annualcostplan_statuscode.Draft && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.None)
                        {
                            _processrecord = false;
                        }

                        Entity preImage = (Entity)context.PreEntityImages["preImage"];
                        _prestatuscode = ((OptionSetValue)(preImage.Attributes["statuscode"])).Value;
                        _preprocessingstatus = ((OptionSetValue)(preImage.Attributes["apd_ibg_processingstatuscode"])).Value;

                        if (_statuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview)
                        {
                            _processrecord = false;
                        }
                        if (_prestatuscode == _statuscode && _preprocessingstatus == _processingstatus)
                        {
                            _processrecord = false;
                        }

                        if (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.Draft && _statuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.None && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview)
                        {
                            _processrecord = false;
                        }

                        //when wsc sends to area office
                        if (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingAreaOfficeReview)
                        {
                            _processrecord = false;
                        }
                        //when area sends to central office
                        if (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingAreaOfficeReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingCentralOfficeReview)
                        {
                            _processrecord = false;
                        }

                        //when area office sends back to WSC
                        if (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingAreaOfficeReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview)
                        {
                            _processrecord = false;
                        }
                        //when central office sends back to area review
                        if (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingCentralOfficeReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingAreaOfficeReview)
                        {
                            _processrecord = false;
                        }
                        #endregion

                        #region consumer does not accept cost plan exception
                        if (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved)
                        {
                            if (HelperClass.HelperClass.GetBooleanValue(original_entity, "apd_consumeracceptscostplan", false) == false
                                && HelperClass.HelperClass.GetStringValue(original_entity, "apd_consumerdoesnotacceptcostplanexplanation") == String.Empty)
                                throw new InvalidPluginExecutionException("Please provider explanation to why the consumer does not accept the cost plan.");
                        }
                        #endregion

                        #region 1st ever cost plan goes to area review

                        if (_isareareviewrequired)
                            _isareareviewrequired_temp = true;
                        var currentapprovedacp = (from c in iBudget.APD_IBG_annualcostplanSet
                                                  where c.apd_consumerid.Id == ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id
                                                  && c.statecode.Value == 0
                                                  && c.statuscode == 4
                                                  && c.APD_ibg_ProcessingStatusCode.Value == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved
                                                  select new { c.APD_IBG_annualcostplanId }).ToList();

                        if (currentapprovedacp.Count() == 0
                            && _prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved)
                        {
                            string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                            original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - 1st ever Cost Plan for this consumer will be automatically sent to Area Office Review" + "\r\n" + apd_reasonforareareview;
                            _isareareviewrequired = true;
                        }
                        #endregion

                        #region retrieve fiscal year details

                        var fiscalyear = (from fy in iBudget.APD_fiscalyearSet
                                          where fy.APD_fiscalyearId == ((EntityReference)original_entity.Attributes["apd_fiscalyearid"]).Id
                                          select new { fy.APD_BeginDate, fy.APD_EndDate, fy.APD_fiscalyearname }).First();

                        #endregion

                        #region retreive all service plans

                        List<APD_IBG_serviceplan> allsps = (from s in iBudget.APD_IBG_serviceplanSet
                                                            where s.apd_annualcostplanid.Id == original_entity.Id
                                                            select s).ToList();
                        #endregion

                        #region retreive all service codes

                        List<APD_servicecode> allsc = (from s in iBudget.APD_servicecodeSet select s).ToList();
                        #endregion

                        #region within FY validations

                        if (_isitacopiedrecord)
                        {
                            #region current approved service plans

                            List<APD_IBG_serviceplan> approved = new List<APD_IBG_serviceplan>();

                            var _cacp = (from a in iBudget.APD_IBG_annualcostplanSet
                                         where a.APD_ibg_ProcessingStatusCode.Value == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved
                                         && a.statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                         && a.apd_consumerid.Id == ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id
                                         && a.apd_fiscalyearid.Id == ((EntityReference)original_entity.Attributes["apd_fiscalyearid"]).Id
                                         select new { a.APD_IBG_annualcostplanId }).ToList().FirstOrDefault();

                            if (_cacp != null)
                            {
                                var _cannualcostplanid = _cacp.APD_IBG_annualcostplanId.Value;

                                approved = (from a in iBudget.APD_IBG_serviceplanSet
                                            where a.apd_annualcostplanid.Id == _cannualcostplanid
                                            select a).ToList();
                            }

                            #endregion

                            #region Validate Critical Services Required

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _scdescription;
                                _isareareviewrequired = !Validate_CriticalServices_Required(iBudget, original_entity, out _scdescription, allsps, allsc);
                                _scdescription = _scdescription.TrimEnd(new char[] { ',', ' ' });

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - One or more of the Consumer's needed services have not been budgeted in the Cost Plan: " + _scdescription + "\r\n" + apd_reasonforareareview;
                                }
                            }

                            #endregion

                            #region Validate Critical Services Current Approved Cost Plan

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved)
                                && _isitacopiedrecord)
                            {
                                string _scdescription;
                                _isareareviewrequired = !Validate_CriticalServices_CurrentApprovedCostPlan(iBudget, original_entity, out _scdescription, allsps, allsc);
                                _scdescription = _scdescription.TrimEnd(new char[] { ',', ' ' });

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - " + _scdescription + " modified. Please check for health and safety." + "\r\n" + apd_reasonforareareview;
                                }
                            }

                            #endregion

                            #region Validate Support Coordination Required

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _months;
                                _isareareviewrequired = !Validate_SupportCoordination_Required(iBudget, original_entity, fiscalyear.APD_fiscalyearname, out _months, allsps, allsc);
                                _months = _months.TrimEnd(new char[] { ',', ' ' });

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - Support Coordination not budgeted as required in the Cost Plan in the following months:" + _months + "\r\n" + apd_reasonforareareview;
                                }
                            }

                            #endregion

                            #region Procedure Code which trigger automatic review

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                _isareareviewrequired = false;

                                string _servicecode_description = "";

                                #region Fetch all Procedure Code which trigger automatic review

                                var auto_triggers = (from a in iBudget.APD_procedurecodeserviceruleSet
                                                     where a.statecode.Value == 0
                                                     && a.APD_TriggersAutomaticAreaReview.Value == 2
                                                     select a).ToList();

                                string _msg = "";

                                var procedure_codes = (from pc in iBudget.APD_procedurecodeSet
                                                       select pc).ToList();

                                if (auto_triggers.Count() > 0)
                                {
                                    foreach (var auto in auto_triggers)
                                    {
                                        #region Get Procedure Code Description for Procedure Code Id

                                        _servicecode_description = procedure_codes.Find(x => x.APD_procedurecodeId.Value == auto.APD_ProcedureCodeId.Id).APD_Description;

                                        #endregion

                                        decimal apd_ibg_totalamount = 0, apd_ibg_ctotalamount = 0;

                                        #region Get Service Plans for Annual Cost Plan Id & Procedure Code

                                        var auto_trigger_sps = (from s in allsps
                                                                where s.apd_annualcostplanid.Id == original_entity.Id
                                                                && s.apd_procedurecodeid.Id == auto.APD_ProcedureCodeId.Id
                                                                select s).ToList();

                                        var auto_trigger_csps = (from s in approved
                                                                 where s.apd_annualcostplanid.Id == original_entity.Id
                                                                 && s.apd_procedurecodeid.Id == auto.APD_ProcedureCodeId.Id
                                                                 select s).ToList();

                                        #endregion

                                        #region If Total > 0, trigger automatic area review

                                        foreach (var ats in auto_trigger_sps)
                                            apd_ibg_totalamount += ats.APD_IBG_TotalAmount.Value;
                                        foreach (var ats in auto_trigger_csps)
                                            apd_ibg_ctotalamount += ats.APD_IBG_TotalAmount.Value;

                                        if (apd_ibg_totalamount > 0 && apd_ibg_totalamount > apd_ibg_ctotalamount)
                                        {
                                            _isareareviewrequired = true;
                                            _msg += _servicecode_description + ", ";
                                        }

                                        #endregion
                                    }
                                }

                                if (_isareareviewrequired)
                                    _isareareviewrequired_temp = true;

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - The following service(s): " + _msg.TrimEnd(new char[] { ',', ' ' }) + " have triggered an automatic area review due to an increase from the previous approved cost plan. Please review accordingly." + "\r\n" + apd_reasonforareareview;
                                }
                                #endregion
                            }
                            #endregion

                            #region Validate Critical Service Groups

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _criticalgroups;
                                _isareareviewrequired = !Validate_CriticalServiceGroups(iBudget, original_entity, fiscalyear.APD_fiscalyearname, out _criticalgroups, allsps, allsc, true);

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - Services from the following critical groups: " + _criticalgroups.TrimEnd(new char[] { ',', ' ' }) + " not budgeted as required." + "\r\n" + apd_reasonforareareview;
                                }
                            }
                            #endregion

                            #region Validate Critical Service Groups Current Approved Cost Plan

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _criticalgroups;
                                _isareareviewrequired = !Validate_CriticalServiceGroups_CurrentApprovedCostPlan(iBudget, original_entity, fiscalyear.APD_fiscalyearname, out _criticalgroups, allsps, allsc, approved);

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - Services from the following critical groups: " + _criticalgroups.TrimEnd(new char[] { ',', ' ' }) + " have been reduced compared to the current approved cost plan." + "\r\n" + apd_reasonforareareview;
                                }
                            }
                            #endregion

                            #region Validate Group Totals - Area Review - Current Approved

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _message;
                                _isareareviewrequired = !Validate_CriticalServiceGroupAOAmounts(iBudget, original_entity, out _message, allsps, allsc, approved);

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - " + _message.TrimEnd(new char[] { ';', ' ' }) + "\r\n" + apd_reasonforareareview;
                                }
                            }
                            #endregion

                            #region Validate Ratios - Area Review

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _message;
                                _isareareviewrequired = !Validate_AORatios(iBudget, original_entity, allsps, out _message, allsc);

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - The following Service, Ratio combination(s) have triggered Area Review: " + _message.TrimEnd(new char[] { ',', ' ' }) + "\r\n" + apd_reasonforareareview;
                                }
                            }
                            #endregion

                            #region Validate Ratios - CO Review

                            if (_iscentralreviewrequired)
                                _iscentralreviewrequired_temp = true;
                            if (!_isareareviewrequired_temp && (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                && (_preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingAreaOfficeReview || _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview) && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _message;
                                _iscentralreviewrequired = !Validate_CORatios(iBudget, original_entity, allsps, out _message, allsc);

                                if (_iscentralreviewrequired)
                                {
                                    string apd_reasonforcentralreview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforcentralreview");
                                    original_entity["apd_reasonforcentralreview"] = DateTime.Now.ToString() + " - The following Service, Ratio combination(s) have triggered Central Office Review: " + _message.TrimEnd(new char[] { ',', ' ' }) + "\r\n" + apd_reasonforcentralreview; ;
                                }
                            }
                            #endregion

                            #region Validate Group Totals - CO Review - Current Approved

                            if (_iscentralreviewrequired)
                                _iscentralreviewrequired_temp = true;
                            if (!_isareareviewrequired_temp && (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                && (_preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingAreaOfficeReview || _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview) && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _message;
                                _iscentralreviewrequired = !Validate_CriticalServiceGroupCOAmounts(iBudget, original_entity, out _message, allsps, allsc, approved);

                                if (_iscentralreviewrequired)
                                {
                                    string apd_reasonforcentralreview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforcentralreview");
                                    original_entity["apd_reasonforcentralreview"] = DateTime.Now.ToString() + " - " + _message.TrimEnd(new char[] { ';', ' ' }) + "\r\n" + apd_reasonforcentralreview; ;
                                }
                            }
                            #endregion
                        }

                        #endregion

                        #region new FY validations

                        if (!_isitacopiedrecord)
                        {
                            #region retreive previous FY current approved service plans

                            List<APD_IBG_serviceplan> previousFYSPs = new List<APD_IBG_serviceplan>();

                            string previousFYname = (Convert.ToInt32(fiscalyear.APD_fiscalyearname.Substring(0, 4)) - 1).ToString() + "-" + (Convert.ToInt32(fiscalyear.APD_fiscalyearname.Substring(5, 4)) - 1).ToString();

                            var previousFY = (from fy in iBudget.APD_fiscalyearSet
                                              where fy.APD_fiscalyearname == previousFYname
                                              select fy).FirstOrDefault();

                            if (previousFY != null)
                            {
                                var pcurrentapproved_cps = (from a in iBudget.APD_IBG_annualcostplanSet
                                                            where a.apd_consumerid.Id == ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id
                                                             && a.apd_fiscalyearid.Id == previousFY.APD_fiscalyearId.Value
                                                             && a.statuscode.Value == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                                             && a.APD_ibg_ProcessingStatusCode.Value == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved
                                                            select a).ToList().FirstOrDefault();

                                if (pcurrentapproved_cps != null)
                                    previousFYSPs = (from sp in iBudget.APD_IBG_serviceplanSet
                                                     where sp.apd_annualcostplanid.Id == pcurrentapproved_cps.APD_IBG_annualcostplanId.Value
                                                     select sp).ToList();
                            }

                            #endregion

                            #region Validate Critical Services Required

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _scdescription;
                                _isareareviewrequired = !Validate_CriticalServices_Required(iBudget, original_entity, out _scdescription, allsps, allsc);
                                _scdescription = _scdescription.TrimEnd(new char[] { ',', ' ' });

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - One or more of the Consumer's needed services have not been budgeted in the Cost Plan: " + _scdescription + "\r\n" + apd_reasonforareareview;
                                }
                            }

                            #endregion

                            #region Validate Support Coordination Required

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _months;
                                _isareareviewrequired = !Validate_SupportCoordination_Required(iBudget, original_entity, fiscalyear.APD_fiscalyearname, out _months, allsps, allsc);
                                _months = _months.TrimEnd(new char[] { ',', ' ' });

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - Support Coordination not budgeted as required in the Cost Plan in the following months:" + _months + "\r\n" + apd_reasonforareareview;
                                }
                            }

                            #endregion

                            #region Procedure Code which trigger automatic review

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                _isareareviewrequired = false;

                                string _servicecode_description = "";

                                #region Fetch all Procedure Code which trigger automatic review

                                var auto_triggers = (from a in iBudget.APD_procedurecodeserviceruleSet
                                                     where a.statecode.Value == 0
                                                     && a.APD_TriggersAutomaticAreaReview.Value == 2
                                                     select a).ToList();

                                string _msg = "";

                                var procedure_codes = (from pc in iBudget.APD_procedurecodeSet
                                                       select pc).ToList();

                                if (auto_triggers.Count() > 0)
                                {
                                    foreach (var auto in auto_triggers)
                                    {
                                        #region Get Procedure Code Description for Procedure Code Id

                                        _servicecode_description = procedure_codes.Find(x => x.APD_procedurecodeId.Value == auto.APD_ProcedureCodeId.Id).APD_Description;

                                        #endregion

                                        double apd_ibg_totalnumberofunits = 0;

                                        #region Get Service Plans for Annual Cost Plan Id & Procedure Code

                                        var auto_trigger_sps = (from s in allsps
                                                                where s.apd_annualcostplanid.Id == original_entity.Id
                                                                && s.apd_procedurecodeid.Id == auto.APD_ProcedureCodeId.Id
                                                                select s).ToList();

                                        #endregion

                                        #region If Total > 0, trigger automatic area review

                                        foreach (var ats in auto_trigger_sps)
                                            apd_ibg_totalnumberofunits += ats.APD_IBG_TotalNumberOfUnits.Value;


                                        if (apd_ibg_totalnumberofunits > 0)
                                        //&& (from s in previousFYSPs where s.apd_procedurecodeid.Id == auto.APD_ProcedureCodeId.Id && s.APD_IBG_TotalAmount.Value > 0 select s).ToList().Count() == 0)
                                        {
                                            _isareareviewrequired = true;

                                            _msg += _servicecode_description + ", ";
                                        }

                                        #endregion
                                    }
                                }

                                if (_isareareviewrequired)
                                    _isareareviewrequired_temp = true;

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - The following service(s): " + _msg.TrimEnd(new char[] { ',', ' ' }) + " have triggered an automatic area review. Please review accordingly." + "\r\n" + apd_reasonforareareview;
                                }
                                #endregion
                            }
                            #endregion

                            #region Validate Critical Services previous FY Current Approved Cost Plan

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved)
                                && !_isitacopiedrecord)
                            {
                                string _scdescription;
                                _isareareviewrequired = !Validate_CriticalServices_PreviousFYCurrentApprovedCostPlan(iBudget, original_entity, out _scdescription, allsps, allsc, previousFYSPs);
                                _scdescription = _scdescription.TrimEnd(new char[] { ',', ' ' });

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - " + _scdescription + " reduced as compared to previous FY. Please check for health and safety." + "\r\n" + apd_reasonforareareview;
                                }
                            }
                            #endregion

                            #region Validate Critical Service Groups

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _criticalgroups;
                                _isareareviewrequired = !Validate_CriticalServiceGroups(iBudget, original_entity, fiscalyear.APD_fiscalyearname, out _criticalgroups, allsps, allsc, false);

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - Services from the following critical groups: " + _criticalgroups.TrimEnd(new char[] { ',', ' ' }) + " not budgeted as required." + "\r\n" + apd_reasonforareareview;
                                }
                            }
                            #endregion

                            #region Validate Group Totals - Area Review

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _message;
                                _isareareviewrequired = !Validate_PreviousFYCriticalServiceGroupAOAmounts(iBudget, original_entity, out _message, allsps, allsc, previousFYSPs);

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - " + _message.TrimEnd(new char[] { ';', ' ' }) + "\r\n" + apd_reasonforareareview;
                                }
                            }
                            #endregion

                            #region Validate Ratios - Area Review

                            if (_isareareviewrequired)
                                _isareareviewrequired_temp = true;
                            if ((_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _message;
                                _isareareviewrequired = !Validate_PreviousFYAORatios(iBudget, original_entity, allsps, out _message, allsc, previousFYSPs);

                                if (_isareareviewrequired)
                                {
                                    string apd_reasonforareareview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforareareview");
                                    original_entity["apd_reasonforareareview"] = DateTime.Now.ToString() + " - The following Service, Ratio combination(s) that don't exist in the previous FY have triggered Area Review: " + _message.TrimEnd(new char[] { ',', ' ' }) + "\r\n" + apd_reasonforareareview;
                                }
                            }
                            #endregion

                            #region Validate Ratios - CO Review

                            if (_iscentralreviewrequired)
                                _iscentralreviewrequired_temp = true;
                            if (!_isareareviewrequired_temp && (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                && (_preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingAreaOfficeReview || _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview) && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _message;
                                _iscentralreviewrequired = !Validate_PreviousFYCORatios(iBudget, original_entity, allsps, out _message, allsc, previousFYSPs);

                                if (_iscentralreviewrequired)
                                {
                                    string apd_reasonforcentralreview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforcentralreview");
                                    original_entity["apd_reasonforcentralreview"] = DateTime.Now.ToString() + " - The following Service, Ratio combination(s) that don't exist in the previous FY have triggered Central Office Review: " + _message.TrimEnd(new char[] { ',', ' ' }) + "\r\n" + apd_reasonforcentralreview; ;
                                }
                            }
                            #endregion

                            #region Validate Group Totals - CO Review - previous FY Current Approved

                            if (_iscentralreviewrequired)
                                _iscentralreviewrequired_temp = true;
                            if (!_isareareviewrequired_temp && (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                && (_preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingAreaOfficeReview || _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview) && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved))
                            {
                                string _message;
                                _iscentralreviewrequired = !Validate_PreviousFYCriticalServiceGroupCOAmounts(iBudget, original_entity, out _message, allsps, allsc, previousFYSPs);

                                if (_iscentralreviewrequired)
                                {
                                    string apd_reasonforcentralreview = HelperClass.HelperClass.GetStringValue(original_entity, "apd_reasonforcentralreview");
                                    original_entity["apd_reasonforcentralreview"] = DateTime.Now.ToString() + " - " + _message.TrimEnd(new char[] { ';', ' ' }) + "\r\n" + apd_reasonforcentralreview; ;
                                }
                            }
                            #endregion
                        }

                        #endregion

                        #region Update Cost Plan totals on change

                        decimal _totalbudgeted = (from s in allsps where s.APD_ibg_IncludeinTotal.Value == true select new { s.APD_IBG_TotalAmount }).ToList().Sum(t => t.APD_IBG_TotalAmount.Value);

                        if (_totalbudgeted > 0)
                        {
                            Decimal _allocatedamount = new Decimal(0);
                            Decimal _cp_yearlybudget = new Decimal(0);
                            Decimal _flexiblespendingamount = new Decimal(0);
                            Decimal _emergencyreserveamount = new Decimal(0);
                            Decimal _supplementalfundamount = new Decimal(0);

                            Decimal _totalbudgetedamount = new Decimal(0);
                            Decimal _budgetedamount = new Decimal(0);
                            Decimal _budgetedflexiblespendingamount = new Decimal(0);
                            Decimal _budgetedemergencyreserveamount = new Decimal(0);
                            Decimal _budgetedsupplementalfundamount = new Decimal(0);

                            Decimal _totalbalanceamount = new Decimal(0);
                            Decimal _balanceamount = new Decimal(0);
                            Decimal _balanceflexiblespendingamount = new Decimal(0);
                            Decimal _balanceemergencyreserveamount = new Decimal(0);
                            Decimal _balancesupplementalfundamount = new Decimal(0);
                            Decimal _tempamount = new Decimal(0);

                            _allocatedamount = ((Money)original_entity["apd_ibg_allocatedamount"]).Value;
                            _cp_yearlybudget = ((Money)original_entity["apd_ibg_yearlybudget"]).Value;
                            _flexiblespendingamount = ((Money)original_entity["apd_ibg_flexiblespendingamount"]).Value;
                            _emergencyreserveamount = ((Money)original_entity["apd_ibg_emergencyreserveamount"]).Value;

                            _totalbudgetedamount = _totalbudgeted;

                            if (_totalbudgetedamount > _allocatedamount)
                                throw new InvalidPluginExecutionException("Service Plan total amount " + _totalbudgetedamount.ToString("$###,###.##") + " exceeds the Cost Plan Allocated amount of " + _allocatedamount.ToString("$###,###.##") + "." + Environment.NewLine + "Please reduce existing Service Plans.");

                            if (_totalbudgetedamount > _cp_yearlybudget)
                            {
                                _tempamount = _totalbudgetedamount - _cp_yearlybudget;
                                _budgetedamount = _cp_yearlybudget;
                                if (_tempamount > _flexiblespendingamount)
                                {
                                    _budgetedflexiblespendingamount = _flexiblespendingamount;
                                    _tempamount = _tempamount - _budgetedflexiblespendingamount;
                                    if (_tempamount > _emergencyreserveamount)
                                    {
                                        _budgetedemergencyreserveamount = _emergencyreserveamount;
                                        _tempamount = _tempamount - _emergencyreserveamount;
                                        _budgetedsupplementalfundamount = _tempamount;
                                    }
                                    else
                                    {
                                        _budgetedemergencyreserveamount = _tempamount;
                                        _budgetedsupplementalfundamount = 0;
                                    }
                                }
                                else
                                {
                                    _budgetedflexiblespendingamount = _tempamount;
                                    _budgetedemergencyreserveamount = 0;
                                    _budgetedsupplementalfundamount = 0;
                                }
                            }
                            else
                            {
                                _budgetedamount = _totalbudgetedamount;
                                _budgetedflexiblespendingamount = 0;
                                _budgetedemergencyreserveamount = 0;
                                _budgetedsupplementalfundamount = 0;
                            }

                            _balanceamount = _cp_yearlybudget - _budgetedamount;
                            _balanceflexiblespendingamount = _flexiblespendingamount - _budgetedflexiblespendingamount;
                            _balanceemergencyreserveamount = _emergencyreserveamount - _budgetedemergencyreserveamount;
                            _balancesupplementalfundamount = _supplementalfundamount - _budgetedsupplementalfundamount;

                            original_entity["apd_ibg_budgetedamount"] = new Money(_budgetedamount);
                            original_entity["apd_ibg_budgetedflexiblespendingamount"] = new Money(_budgetedflexiblespendingamount);
                            original_entity["apd_ibg_budgetedemergencyreserveamount"] = new Money(_budgetedemergencyreserveamount);
                            original_entity["apd_ibg_budgetedsupplementalfundamount"] = new Money(0.0M);
                            original_entity["apd_ibg_totalbudgetedamount"] = new Money(_totalbudgetedamount);

                            original_entity["apd_ibg_balanceamount"] = new Money(_balanceamount);
                            original_entity["apd_ibg_balanceflexiblespendingamount"] = new Money(_balanceflexiblespendingamount);
                            original_entity["apd_ibg_balanceemergencyreserveamount"] = new Money(_balanceemergencyreserveamount);
                            original_entity["apd_ibg_balancesupplementalfundamount"] = new Money(_balancesupplementalfundamount);
                            original_entity["apd_ibg_remainingbalanceamount"] = new Money(_balanceamount + _balanceflexiblespendingamount + _balanceemergencyreserveamount + _balancesupplementalfundamount);

                        }

                        #endregion

                        #region set to AO/ CO status as required

                        if (_isareareviewrequired)
                            _isareareviewrequired_temp = true;
                        if (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved && _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview
                            && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved)
                        {
                            if (_isareareviewrequired_temp == true)
                            {
                                OptionSetValue statuscode = new OptionSetValue(3);
                                OptionSetValue apd_ibg_processingstatuscode = new OptionSetValue(3);

                                original_entity.Attributes["apd_ibg_requiresareaofficereviewi"] = true;
                                original_entity.Attributes["statuscode"] = statuscode;
                                original_entity.Attributes["apd_ibg_processingstatuscode"] = apd_ibg_processingstatuscode;
                                _processrecord = false;
                            }
                        }
                        if (_iscentralreviewrequired)
                            _iscentralreviewrequired_temp = true;
                        if (_prestatuscode == (int)apd_ibg_annualcostplan_statuscode.PendingReview && _statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                            && (_preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingAreaOfficeReview || _preprocessingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.PendingWSCReview) && _processingstatus == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved)
                        {
                            if (_iscentralreviewrequired_temp == true)
                            {
                                OptionSetValue statuscode = new OptionSetValue();
                                OptionSetValue apd_ibg_processingstatuscode = new OptionSetValue();

                                statuscode.Value = 3;
                                apd_ibg_processingstatuscode.Value = 4;

                                original_entity["apd_ibg_requirescentralofficereview"] = true;
                                original_entity["statuscode"] = statuscode;
                                original_entity["apd_ibg_processingstatuscode"] = apd_ibg_processingstatuscode;
                                _processrecord = false;
                            }
                        }
                        #endregion

                        #region Process record = true validations

                        if (_processrecord == true)
                        {
                            decimal serviceplan_total = 0.0M; Double total_units = 0.0;

                            string formatXml = string.Empty;

                            string fetchXml = @"<fetch mapping='logical' output-format='xml-platform' no-lock='true' distinct='true' version='1.0' >
                                            <entity name='apd_ibg_serviceplan' >
                                                <attribute name='apd_ibg_serviceplanid' />
                                                <attribute name='apd_ibg_totalamount' />
                                                <attribute name='apd_ibg_totalnumberofunits' />
                                                <attribute name='apd_ibg_includeintotal' />
                                                <attribute name='apd_servicecodeid' />
                                                <order descending='false' attribute='apd_ibg_serviceplanid' />
                                                <filter type='and' >
                                                    <condition attribute='apd_annualcostplanid' value='{0}' operator='eq' />                                                    
                                                </filter>
                                            </entity>
                                        </fetch>";

                            //  pass user guid as parameter
                            formatXml = string.Format(fetchXml, original_entity.Id.ToString());

                            if (formatXml != String.Empty)
                            {

                                #region check if service plans exist

                                // Executing fetchxml using  RetrieveMultiple method
                                EntityCollection entities = service.RetrieveMultiple(new FetchExpression(formatXml));

                                foreach (Entity e in entities.Entities)
                                {
                                    if ((Boolean)e.Attributes["apd_ibg_includeintotal"])
                                        serviceplan_total += !e.Attributes.Contains("apd_ibg_totalamount") ? 0.0M : (e.Attributes["apd_ibg_totalamount"] != null ? ((Money)e.Attributes["apd_ibg_totalamount"]).Value : 0.0M);

                                    total_units += !e.Attributes.Contains("apd_ibg_totalnumberofunits") ? 0.0 : (e.Attributes["apd_ibg_totalnumberofunits"] != null ? ((Double)e.Attributes["apd_ibg_totalnumberofunits"]) : 0.0);
                                }

                                if (entities.Entities.Count() == 0)
                                {
                                    _processrecord = false;
                                    throw new InvalidPluginExecutionException("Cost Plan cannot be processed as no service plans exist for this cost plan.");
                                }

                                if (entities.Entities.Count() > 0 && total_units == 0.0)
                                {
                                    _processrecord = false;
                                    throw new InvalidPluginExecutionException("Cost Plan cannot be processed as service plans exist with no monthly details.");
                                }

                                #endregion

                                #region code to check amount exceeds while processing

                                if (serviceplan_total > ((Money)original_entity["apd_ibg_allocatedamount"]).Value)
                                    throw new InvalidPluginExecutionException("Cost Plan cannot be processed since the total of Service Plans exceeds the Cost Plan allocated amount.");

                                var ab_allocatedamount = (from ab in iBudget.APD_IBG_annualbudgetSet
                                                          where ab.apd_consumerid.Id == ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id
                                                          && ab.apd_fiscalyearid.Id == ((EntityReference)original_entity.Attributes["apd_fiscalyearid"]).Id
                                                          select ab).ToList().FirstOrDefault().APD_IBG_AllocatedAmount.Value;

                                if (serviceplan_total > ab_allocatedamount)
                                    throw new InvalidPluginExecutionException("Cost Plan cannot be processed since the total of Service Plans exceeds the Annual Budget allocated amount.");
                                #endregion

                                #region Check to see if all the services in the cost plan are approved

                                string error_desc = "";

                                foreach (Entity e in entities.Entities)
                                {
                                    if ((Double)e.Attributes["apd_ibg_totalnumberofunits"] > 0.0)
                                    {
                                        var caps = (from a in iBudget.APD_IBG_consumerapprovedserviceSet
                                                    where a.apd_servicecodeid.Id == ((EntityReference)e.Attributes["apd_servicecodeid"]).Id
                                                    && a.apd_consumerid.Id == ((EntityReference)original_entity["apd_consumerid"]).Id
                                                    select a).ToList();

                                        if (caps.Count() == 0)
                                            error_desc += (from sc in allsc
                                                           where sc.APD_servicecodeId.Value == ((EntityReference)e.Attributes["apd_servicecodeid"]).Id
                                                           select new { sc.APD_Description }).FirstOrDefault().APD_Description + "), ";

                                    }
                                }

                                if (error_desc.Length > 0)
                                    throw new InvalidPluginExecutionException("The following Service(s) in the Cost Plan are not approved: " + error_desc.TrimEnd(new Char[] { ' ' }).TrimEnd(new Char[] { ',' }) + "; So the Cost Plan cannot be processed.");

                                #endregion

                            }
                        }

                        #endregion

                        #region Check to see if there is a -ve Balance in Reserve Funds, Supplemental Funds

                        if (_processrecord == true)
                        {
                            Decimal apd_ibg_balanceamount = HelperClass.HelperClass.GetMoneyValue(original_entity, "apd_ibg_balanceamount");
                            Decimal apd_ibg_balanceflexiblespendingamount = HelperClass.HelperClass.GetMoneyValue(original_entity, "apd_ibg_balanceflexiblespendingamount");
                            Decimal apd_ibg_balanceemergencyreserveamount = HelperClass.HelperClass.GetMoneyValue(original_entity, "apd_ibg_balanceemergencyreserveamount");
                            Decimal apd_ibg_balancesupplementalfundamount = HelperClass.HelperClass.GetMoneyValue(original_entity, "apd_ibg_balancesupplementalfundamount");

                            if (apd_ibg_balanceamount < 0 || apd_ibg_balanceflexiblespendingamount < 0 || apd_ibg_balanceemergencyreserveamount < 0 || apd_ibg_balancesupplementalfundamount < 0)
                            {
                                throw new InvalidPluginExecutionException("This Annual Cost Plan cannot be processed since there's a -ve balance; Please reduce the Service Plan(s) to reflect the Balance & then Process the Cost Plan");
                            }
                        }
                        #endregion

                        #region Validate Rules

                        if (_processrecord == true)
                        {
                            Validate_Rules(iBudget, original_entity, fiscalyear.APD_fiscalyearname, allsps, allsc);

                            Validate_4421(iBudget, original_entity, allsps, allsc);
                        }

                        #endregion

                        #region Updates any existing Current Approved Cost Plan to Historical Status

                        if (_processrecord == true)
                        {
                            var currentapproved_cps = (from a in iBudget.APD_IBG_annualcostplanSet
                                                       where a.apd_consumerid.Id == ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id
                                                        && a.apd_fiscalyearid.Id == ((EntityReference)original_entity.Attributes["apd_fiscalyearid"]).Id
                                                        && a.statuscode.Value == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                                        && a.APD_ibg_ProcessingStatusCode.Value == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved
                                                       select a).ToList().FirstOrDefault();
                            if (currentapproved_cps != null)
                            {
                                SetStateRequest deactivateRequest = new SetStateRequest
                                {
                                    EntityMoniker =
                                        new EntityReference(APD_IBG_annualcostplan.EntityLogicalName, currentapproved_cps.APD_IBG_annualcostplanId.Value),
                                    Status = new OptionSetValue((int)apd_ibg_annualcostplan_statuscode.Historical),
                                    State = new OptionSetValue(1)
                                };
                                service.Execute(deactivateRequest);
                            }
                        }
                        #endregion

                        #region process = true code for service authorization

                        if (_processrecord == true)
                        {
                            Validate_SP_Quarter_BeginDates(iBudget, allsps);

                            foreach (APD_IBG_serviceplan s in allsps)
                            {
                                if (!s.APD_ibg_isserviceplancopied.Value)
                                {
                                    if (s.APD_IBG_TotalNumberOfUnits.Value == 0)
                                        throw new InvalidPluginExecutionException("Please make sure you enter units for at least 1 month for " + s.APD_IBG_ServicePlanName + " and then try to process the cost plan again.");

                                    bool Q1exists, Q2exists, Q3exists, Q4exists;
                                    _ReturnBeginEndDates(s, out Q1exists, out Q2exists, out Q3exists, out Q4exists);

                                    if (Q1exists)
                                        _CreateServiceAuthorization(iBudget, original_entity, s, 1, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, allsc);

                                    if (Q2exists)
                                        _CreateServiceAuthorization(iBudget, original_entity, s, 2, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, allsc);

                                    if (Q3exists)
                                        _CreateServiceAuthorization(iBudget, original_entity, s, 3, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, allsc);

                                    if (Q4exists)
                                        _CreateServiceAuthorization(iBudget, original_entity, s, 4, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, allsc);

                                }
                                else
                                {
                                    string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false' no-lock='false'>
                                                          <entity name='apd_ibg_serviceauthorization'>
                                                            <attribute name='apd_ibg_serviceauthorizationid' />
                                                            <attribute name='apd_ibg_quartercode' />
                                                            <order attribute='apd_ibg_quartercode' descending='false' />
                                                            <filter type='and'>
                                                                <condition attribute='apd_ibg_serviceplanid' operator='eq' value='{0}' />
                                                                <condition attribute='statecode' operator='eq' value='0' />
                                                            </filter>
                                                          </entity>
                                                        </fetch>";

                                    //  pass user guid as parameter
                                    string formatXml = string.Format(fetchXml, s.APD_OldServicePlanId.Id);

                                    // Executing fetchxml using  RetrieveMultiple method
                                    EntityCollection entities = service.RetrieveMultiple(new FetchExpression(formatXml));

                                    bool Q1exists, Q2exists, Q3exists, Q4exists;
                                    _ReturnBeginEndDates(s, out Q1exists, out Q2exists, out Q3exists, out Q4exists);

                                    Entity sa1 = null;
                                    if (entities.Entities.Count() > 0)
                                        sa1 = entities.Entities.ToList().FindAll(q => q.GetAttributeValue<OptionSetValue>("apd_ibg_quartercode").Value == (int)APD_IBG_serviceauthorizationAPD_IBG_QuarterCode.JulySeptember).FirstOrDefault();

                                    if (sa1 != null)
                                    {
                                        if (Q1exists && sa1.Attributes["apd_ibg_serviceauthorizationid"] != null)
                                            _UpdateServiceAuthorization(iBudget, original_entity, s, sa1.GetAttributeValue<Guid>("apd_ibg_serviceauthorizationid"), 1, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, service, allsc);
                                    }
                                    else if (Q1exists && sa1 == null &&
                                        (s.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                        s.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit))
                                        _CreateServiceAuthorization(iBudget, original_entity, s, 1, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, allsc);

                                    Entity sa2 = null;
                                    if (entities.Entities.Count() > 0)
                                        sa2 = entities.Entities.ToList().FindAll(q => q.GetAttributeValue<OptionSetValue>("apd_ibg_quartercode").Value == (int)APD_IBG_serviceauthorizationAPD_IBG_QuarterCode.OctoberDecember).FirstOrDefault();

                                    if (sa2 != null)
                                    {
                                        if (Q2exists && sa2.Attributes["apd_ibg_serviceauthorizationid"] != null)
                                            _UpdateServiceAuthorization(iBudget, original_entity, s, sa2.GetAttributeValue<Guid>("apd_ibg_serviceauthorizationid"), 2, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, service, allsc);
                                    }
                                    else if (Q2exists && sa2 == null &&
                                        (s.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                        s.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit))
                                        _CreateServiceAuthorization(iBudget, original_entity, s, 2, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, allsc);

                                    Entity sa3 = null;
                                    if (entities.Entities.Count() > 0)
                                        sa3 = entities.Entities.ToList().FindAll(q => q.GetAttributeValue<OptionSetValue>("apd_ibg_quartercode").Value == (int)APD_IBG_serviceauthorizationAPD_IBG_QuarterCode.JanuaryMarch).FirstOrDefault();

                                    if (sa3 != null)
                                    {
                                        if (Q3exists && sa3.Attributes["apd_ibg_serviceauthorizationid"] != null)
                                            _UpdateServiceAuthorization(iBudget, original_entity, s, sa3.GetAttributeValue<Guid>("apd_ibg_serviceauthorizationid"), 3, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, service, allsc);
                                    }
                                    else if (Q3exists && sa3 == null &&
                                        (s.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                        s.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit))
                                        _CreateServiceAuthorization(iBudget, original_entity, s, 3, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, allsc);

                                    Entity sa4 = null;
                                    if (entities.Entities.Count() > 0)
                                        sa4 = entities.Entities.ToList().FindAll(q => q.GetAttributeValue<OptionSetValue>("apd_ibg_quartercode").Value == (int)APD_IBG_serviceauthorizationAPD_IBG_QuarterCode.AprilJune).FirstOrDefault();

                                    if (sa4 != null)
                                    {
                                        if (Q4exists && sa4.Attributes["apd_ibg_serviceauthorizationid"] != null)
                                            _UpdateServiceAuthorization(iBudget, original_entity, s, sa4.GetAttributeValue<Guid>("apd_ibg_serviceauthorizationid"), 4, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, service, allsc);
                                    }
                                    else if (Q4exists && sa4 == null &&
                                        (s.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                        s.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit))
                                        _CreateServiceAuthorization(iBudget, original_entity, s, 4, ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id, consumer.apd_waiversupportcoordinatorid.Id, allsc);


                                }
                            }
                        }

                        #endregion
                    }
                }
                #endregion
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("An error occurred in the plug-in.", ex);
            }
        }

        private bool Validate_CORatios(XrmServiceContext iBudget, Entity original_entity, List<APD_IBG_serviceplan> allsps, out string _message, List<APD_servicecode> allsc)
        {
            bool isValid = true;
            _message = "";
            var rateset = (from r in iBudget.APD_rateSet
                           where r.apd_TriggersAutomaticCentralReview.Value == (int)apd_yesorno.Yes
                           select r).ToList();

            foreach (APD_IBG_serviceplan s in allsps)
            {
                if (!s.APD_ibg_isserviceplancopied.Value)
                    if (s.APD_IBG_TotalAmount.Value > 0)
                    {
                        string sc_description = (from sc in allsc where sc.APD_servicecodeId.Value == s.apd_servicecodeid.Id select new { sc.APD_Description }).FirstOrDefault().APD_Description;
                        if (((CrmEntityReference)s.APD_JulyServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JulyServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_AugustServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_AugustServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_SeptemberServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_SeptemberServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_OctoberServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_OctoberServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_NovemberServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_NovemberServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_DecemberServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_DecemberServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_JanuaryServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JanuaryServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_FebruaryServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_FebruaryServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_MarchServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_MarchServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_AprilServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_AprilServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_MayServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_MayServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                        if (((CrmEntityReference)s.APD_JuneServiceRateId) != null)
                        {
                            if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JuneServiceRateId).Id select r).ToList().Count() > 0)
                            {
                                isValid = false;
                                _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                                break;
                            }
                        }
                    }
                if (_message.Length > 0)
                    _message += ", ";
            }

            return isValid;
        }

        private bool Validate_PreviousFYCORatios(XrmServiceContext iBudget, Entity original_entity, List<APD_IBG_serviceplan> allsps, out string _message, List<APD_servicecode> allsc, List<APD_IBG_serviceplan> previousFYSps)
        {
            bool isValid = true;
            _message = "";
            var rateset = (from r in iBudget.APD_rateSet
                           where r.apd_TriggersAutomaticCentralReview.Value == (int)apd_yesorno.Yes
                           select r).ToList();

            foreach (APD_IBG_serviceplan s in allsps)
            {
                if (s.APD_IBG_TotalAmount.Value > 0)
                {
                    string sc_description = (from sc in allsc where sc.APD_servicecodeId.Value == s.apd_servicecodeid.Id select new { sc.APD_Description }).FirstOrDefault().APD_Description;
                    if (((CrmEntityReference)s.APD_JulyServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JulyServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_JulyServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_AugustServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_AugustServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_AugustServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_SeptemberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_SeptemberServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_SeptemberServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_OctoberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_OctoberServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_OctoberServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_NovemberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_NovemberServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_NovemberServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_DecemberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_DecemberServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_DecemberServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_JanuaryServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JanuaryServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_JanuaryServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_FebruaryServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_FebruaryServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_FebruaryServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_MarchServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_MarchServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_MarchServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_AprilServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_AprilServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_AprilServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_MayServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_MayServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_MayServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_JuneServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JuneServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_JuneServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                }
                if (_message.Length > 0)
                    _message += ", ";
            }

            return isValid;
        }

        private bool Validate_AORatios(XrmServiceContext iBudget, Entity original_entity, List<APD_IBG_serviceplan> allsps, out string _message, List<APD_servicecode> allsc)
        {
            bool isValid = true;
            _message = "";
            var rateset = (from r in iBudget.APD_rateSet
                           where r.apd_TriggersAutomaticAreaReview.Value == (int)apd_yesorno.Yes
                           select r).ToList();

            foreach (APD_IBG_serviceplan s in allsps)
            {
                if (s.APD_IBG_TotalAmount.Value > 0)
                {
                    string sc_description = (from sc in allsc where sc.APD_servicecodeId.Value == s.apd_servicecodeid.Id select new { sc.APD_Description }).FirstOrDefault().APD_Description;
                    if (((CrmEntityReference)s.APD_JulyServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JulyServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_AugustServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_AugustServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_SeptemberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_SeptemberServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_OctoberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_OctoberServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_NovemberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_NovemberServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_DecemberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_DecemberServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_JanuaryServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JanuaryServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_FebruaryServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_FebruaryServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_MarchServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_MarchServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_AprilServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_AprilServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_MayServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_MayServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_JuneServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JuneServiceRateId).Id select r).ToList().Count() > 0)
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                }
                if (_message.Length > 0)
                    _message += ", ";
            }

            return isValid;
        }

        private bool Validate_PreviousFYAORatios(XrmServiceContext iBudget, Entity original_entity, List<APD_IBG_serviceplan> allsps, out string _message, List<APD_servicecode> allsc, List<APD_IBG_serviceplan> previousFYSps)
        {
            bool isValid = true;
            _message = "";
            var rateset = (from r in iBudget.APD_rateSet
                           where r.apd_TriggersAutomaticAreaReview.Value == (int)apd_yesorno.Yes
                           select r).ToList();

            foreach (APD_IBG_serviceplan s in allsps)
            {
                if (s.APD_IBG_TotalAmount.Value > 0)
                {
                    string sc_description = (from sc in allsc where sc.APD_servicecodeId.Value == s.apd_servicecodeid.Id select new { sc.APD_Description }).FirstOrDefault().APD_Description;
                    if (((CrmEntityReference)s.APD_JulyServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JulyServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_JulyServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_AugustServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_AugustServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_AugustServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_SeptemberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_SeptemberServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_SeptemberServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_OctoberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_OctoberServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_OctoberServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_NovemberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_NovemberServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_NovemberServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_DecemberServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_DecemberServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_DecemberServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_JanuaryServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JanuaryServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_JanuaryServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_FebruaryServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_FebruaryServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_FebruaryServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_MarchServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_MarchServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_MarchServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_AprilServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_AprilServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_AprilServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_MayServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_MayServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_MayServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                    if (((CrmEntityReference)s.APD_JuneServiceRateId) != null)
                    {
                        if ((from r in rateset where r.APD_rateId.Value == ((CrmEntityReference)s.APD_JuneServiceRateId).Id select r).ToList().Count() > 0
                            && !Validate_PreviousFYRatios_CheckPreviousSps(s.APD_JuneServiceRateId.Id, previousFYSps, s.apd_procedurecodeid.Id))
                        {
                            isValid = false;
                            _message = sc_description + " - " + s.apd_serviceratioid.Name.ToString();
                            break;
                        }
                    }
                }
                if (_message.Length > 0)
                    _message += ", ";
            }

            return isValid;
        }

        private bool Validate_PreviousFYRatios_CheckPreviousSps(Guid rateId, List<APD_IBG_serviceplan> previousFYSps, Guid apd_procedurecodeid)
        {
            foreach (APD_IBG_serviceplan s in previousFYSps.FindAll(x => x.apd_procedurecodeid.Id == apd_procedurecodeid && x.APD_IBG_TotalAmount.Value > 0))
            {
                if (s.APD_JulyServiceRateId != null)
                    if (((CrmEntityReference)s.APD_JulyServiceRateId).Id == rateId)
                        return true;
                if (s.APD_AugustServiceRateId != null)
                    if (((CrmEntityReference)s.APD_AugustServiceRateId).Id == rateId)
                        return true;
                if (s.APD_SeptemberServiceRateId != null)
                    if (((CrmEntityReference)s.APD_SeptemberServiceRateId).Id == rateId)
                        return true;
                if (s.APD_OctoberServiceRateId != null)
                    if (((CrmEntityReference)s.APD_OctoberServiceRateId).Id == rateId)
                        return true;
                if (s.APD_NovemberServiceRateId != null)
                    if (((CrmEntityReference)s.APD_NovemberServiceRateId).Id == rateId)
                        return true;
                if (s.APD_DecemberServiceRateId != null)
                    if (((CrmEntityReference)s.APD_DecemberServiceRateId).Id == rateId)
                        return true;
                if (s.APD_JanuaryServiceRateId != null)
                    if (((CrmEntityReference)s.APD_JanuaryServiceRateId).Id == rateId)
                        return true;
                if (s.APD_FebruaryServiceRateId != null)
                    if (((CrmEntityReference)s.APD_FebruaryServiceRateId).Id == rateId)
                        return true;
                if (s.APD_MarchServiceRateId != null)
                    if (((CrmEntityReference)s.APD_MarchServiceRateId).Id == rateId)
                        return true;
                if (s.APD_AprilServiceRateId != null)
                    if (((CrmEntityReference)s.APD_AprilServiceRateId).Id == rateId)
                        return true;
                if (s.APD_MayServiceRateId != null)
                    if (((CrmEntityReference)s.APD_MayServiceRateId).Id == rateId)
                        return true;
                if (s.APD_JuneServiceRateId != null)
                    if (((CrmEntityReference)s.APD_JuneServiceRateId).Id == rateId)
                        return true;
            }
            return false;
        }

        private bool Validate_AOServiceTotals(XrmServiceContext iBudget, Entity original_entity, List<APD_IBG_serviceplan> allsps, out string _message, List<APD_servicecode> allsc)
        {
            _message = "";
            bool isValid = true;

            var sc_rules = (from scr in iBudget.APD_servicecoderuleSet
                            where scr.statecode.Value == 0
                            && scr.apd_AreaOfficeApprovalAmounts != "None"
                            select scr).ToList();

            if (sc_rules.Count() > 0)
            {
                foreach (var scr in sc_rules)
                {
                    string _servicecode_description = (from p in allsc
                                                       where p.APD_servicecodeId.Value == scr.APD_ServiceCodeId.Id
                                                       select new { p.APD_Description }).FirstOrDefault().APD_Description;

                    string range = scr.apd_AreaOfficeApprovalAmounts;

                    if (!range.ToLower().Contains("none"))
                    {
                        Decimal lowerLimit = Convert.ToDecimal(range.Substring(0, range.IndexOf("-")).Replace("$", "").Replace(",", ""));
                        Decimal upperLimit = Convert.ToDecimal(range.Substring(range.IndexOf("-") + 1, range.Length - 1 - range.IndexOf("-")).Replace("$", "").Replace(",", ""));

                        #region Get Service Plans for Annual Cost Plan Id & Service Code Id matching Support Coordination

                        var matchedsps = (from s in allsps
                                          where s.apd_servicecodeid.Id == scr.APD_ServiceCodeId.Id
                                          select s).ToList();

                        #endregion

                        if (matchedsps.Count() > 0)
                        {
                            Decimal total = 0;

                            //Calculate the totals of each of the Service Plans
                            foreach (var s in matchedsps)
                            {
                                total += s.APD_IBG_TotalAmount.Value;
                            }

                            if (total >= lowerLimit && total <= upperLimit)
                            {
                                isValid = false;
                                _message = "Current Total of $" + total.ToString("N0") + " for " + _servicecode_description + " exceeds the threshold limits of " + scr.apd_AreaOfficeApprovalAmounts + Environment.NewLine;
                            }
                        }
                    }
                }
            }

            return isValid;
        }

        private bool Validate_COServiceTotals(XrmServiceContext iBudget, Entity original_entity, List<APD_IBG_serviceplan> allsps, out string _message, List<APD_servicecode> allsc)
        {
            _message = "";
            bool isValid = true;

            var sc_rules = (from scr in iBudget.APD_servicecoderuleSet
                            where scr.statecode.Value == 0
                            && scr.apd_StateOfficeApprovalAmounts != "None"
                            select scr).ToList();

            if (sc_rules.Count() > 0)
            {
                foreach (var scr in sc_rules)
                {
                    string _servicecode_description = (from p in allsc
                                                       where p.APD_servicecodeId.Value == scr.APD_ServiceCodeId.Id
                                                       select new { p.APD_Description }).FirstOrDefault().APD_Description;

                    string range = scr.apd_StateOfficeApprovalAmounts;

                    if (!range.ToLower().Contains("none"))
                    {
                        Decimal lowerLimit = Convert.ToDecimal(range.Substring(0, range.IndexOf("-")).Replace("$", "").Replace(",", ""));
                        Decimal upperLimit = Convert.ToDecimal(range.Substring(range.IndexOf("-") + 1, range.Length - 1 - range.IndexOf("-")).Replace("$", "").Replace(",", ""));

                        #region Get Service Plans for Annual Cost Plan Id & Service Code Id matching Support Coordination

                        var matchedsps = (from s in allsps
                                          where s.apd_servicecodeid.Id == scr.APD_ServiceCodeId.Id
                                          select s).ToList();

                        #endregion

                        if (matchedsps.Count() > 0)
                        {
                            Decimal total = 0;

                            //Calculate the totals of each of the Service Plans
                            foreach (var s in matchedsps)
                            {
                                total += s.APD_IBG_TotalAmount.Value;
                            }

                            if (total >= lowerLimit && total <= upperLimit)
                            {
                                isValid = false;
                                _message = "Current Total of $" + total.ToString("N0") + " for " + _servicecode_description + " exceeds the threshold limits of " + scr.apd_StateOfficeApprovalAmounts + Environment.NewLine;
                            }
                        }
                    }
                }
            }

            return isValid;
        }

        private void Validate_SP_Quarter_BeginDates(XrmServiceContext iBudget, List<APD_IBG_serviceplan> allsps)
        {
            string _message = "";

            foreach (APD_IBG_serviceplan sp in allsps)
            {
                if (sp.APD_ibg_isserviceplancopied.HasValue)
                    if (sp.APD_ibg_isserviceplancopied.Value)
                    {
                        if (sp.APD_ibg_SPStatusQ1.HasValue)
                            if (sp.APD_ibg_SPStatusQ1.Value != (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Cancel &&
                                   sp.APD_ibg_PAStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ1.PendingTransmit)
                            {
                                var SA = (from s in iBudget.APD_IBG_serviceauthorizationSet
                                          where s.statecode.Value == 0
                                          && s.APD_IBG_QuarterCode.Value == (int)APD_IBG_serviceauthorizationAPD_IBG_QuarterCode.JulySeptember
                                          && s.apd_ibg_serviceplanid.Id == sp.APD_OldServicePlanId.Id
                                          select new
                                          {
                                              s.APD_PAStatus,
                                              s.APD_IBG_BeginDate,
                                              s.APD_IBG_PANumber
                                          }).ToList().FirstOrDefault();

                                if (SA != null)
                                {
                                    if (!String.IsNullOrWhiteSpace(SA.APD_IBG_PANumber))
                                    {
                                        DateTime APD_IBG_BeginDate = Convert.ToDateTime((sp.APD_IBG_JulUnitsBeginDate.HasValue ? sp.APD_IBG_JulUnitsBeginDate.Value : (sp.APD_IBG_AugUnitsBeginDate.HasValue ? sp.APD_IBG_AugUnitsBeginDate.Value : (sp.APD_IBG_SepUnitsBeginDate.HasValue ? sp.APD_IBG_SepUnitsBeginDate.Value : DateTime.MinValue))).ToShortDateString());

                                        if (APD_IBG_BeginDate != Convert.ToDateTime(SA.APD_IBG_BeginDate.Value.ToShortDateString()))
                                            if (!_message.Contains(sp.APD_IBG_ServicePlanName.Trim()))
                                                _message += sp.APD_IBG_ServicePlanName.Trim() + ", ";
                                    }
                                }
                            }

                        if (sp.APD_ibg_SPStatusQ2.HasValue)
                            if (sp.APD_ibg_SPStatusQ2.Value != (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Cancel &&
                                   sp.APD_ibg_PAStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ2.PendingTransmit)
                            {
                                var SA = (from s in iBudget.APD_IBG_serviceauthorizationSet
                                          where s.statecode.Value == 0
                                          && s.APD_IBG_QuarterCode.Value == (int)APD_IBG_serviceauthorizationAPD_IBG_QuarterCode.OctoberDecember
                                          && s.apd_ibg_serviceplanid.Id == sp.APD_OldServicePlanId.Id
                                          select new
                                          {
                                              s.APD_PAStatus,
                                              s.APD_IBG_BeginDate,
                                              s.APD_IBG_PANumber
                                          }).ToList().FirstOrDefault();

                                if (SA != null)
                                {
                                    if (!String.IsNullOrWhiteSpace(SA.APD_IBG_PANumber))
                                    {
                                        DateTime APD_IBG_BeginDate = Convert.ToDateTime((sp.APD_IBG_OctUnitsBeginDate.HasValue ? sp.APD_IBG_OctUnitsBeginDate.Value : (sp.APD_IBG_NovUnitsBeginDate.HasValue ? sp.APD_IBG_NovUnitsBeginDate.Value : (sp.APD_IBG_DecUnitsBeginDate.HasValue ? sp.APD_IBG_DecUnitsBeginDate.Value : DateTime.MinValue))).ToShortDateString());

                                        if (APD_IBG_BeginDate != Convert.ToDateTime(SA.APD_IBG_BeginDate.Value.ToShortDateString()))
                                            if (!_message.Contains(sp.APD_IBG_ServicePlanName.Trim()))
                                                _message += sp.APD_IBG_ServicePlanName.Trim() + ", ";
                                    }
                                }
                            }

                        if (sp.APD_ibg_SPStatusQ3.HasValue)
                            if (sp.APD_ibg_SPStatusQ3.Value != (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Cancel &&
                                   sp.APD_ibg_PAStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ3.PendingTransmit)
                            {
                                var SA = (from s in iBudget.APD_IBG_serviceauthorizationSet
                                          where s.statecode.Value == 0
                                          && s.APD_IBG_QuarterCode.Value == (int)APD_IBG_serviceauthorizationAPD_IBG_QuarterCode.JanuaryMarch
                                          && s.apd_ibg_serviceplanid.Id == sp.APD_OldServicePlanId.Id
                                          select new
                                          {
                                              s.APD_PAStatus,
                                              s.APD_IBG_BeginDate,
                                              s.APD_IBG_PANumber
                                          }).ToList().FirstOrDefault();

                                if (SA != null)
                                {
                                    if (!String.IsNullOrWhiteSpace(SA.APD_IBG_PANumber))
                                    {
                                        DateTime APD_IBG_BeginDate = Convert.ToDateTime((sp.APD_IBG_JanUnitsBeginDate.HasValue ? sp.APD_IBG_JanUnitsBeginDate.Value : (sp.APD_IBG_FebUnitsBeginDate.HasValue ? sp.APD_IBG_FebUnitsBeginDate.Value : (sp.APD_IBG_MarUnitsBeginDate.HasValue ? sp.APD_IBG_MarUnitsBeginDate.Value : DateTime.MinValue))).ToShortDateString());

                                        if (APD_IBG_BeginDate != Convert.ToDateTime(SA.APD_IBG_BeginDate.Value.ToShortDateString()))
                                            if (!_message.Contains(sp.APD_IBG_ServicePlanName.Trim()))
                                                _message += sp.APD_IBG_ServicePlanName.Trim() + ", ";
                                    }
                                }
                            }

                        if (sp.APD_ibg_SPStatusQ4.HasValue)
                            if (sp.APD_ibg_SPStatusQ4.Value != (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Cancel &&
                                   sp.APD_ibg_PAStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ4.PendingTransmit)
                            {
                                var SA = (from s in iBudget.APD_IBG_serviceauthorizationSet
                                          where s.statecode.Value == 0
                                          && s.APD_IBG_QuarterCode.Value == (int)APD_IBG_serviceauthorizationAPD_IBG_QuarterCode.AprilJune
                                          && s.apd_ibg_serviceplanid.Id == sp.APD_OldServicePlanId.Id
                                          select new
                                          {
                                              s.APD_PAStatus,
                                              s.APD_IBG_BeginDate,
                                              s.APD_IBG_PANumber
                                          }).ToList().FirstOrDefault();

                                if (SA != null)
                                {
                                    if (!String.IsNullOrWhiteSpace(SA.APD_IBG_PANumber))
                                    {
                                        DateTime APD_IBG_BeginDate = Convert.ToDateTime((sp.APD_IBG_AprUnitsBeginDate.HasValue ? sp.APD_IBG_AprUnitsBeginDate.Value : (sp.APD_IBG_MayUnitsBeginDate.HasValue ? sp.APD_IBG_MayUnitsBeginDate.Value : (sp.APD_IBG_JunUnitsBeginDate.HasValue ? sp.APD_IBG_JunUnitsBeginDate.Value : DateTime.MinValue))).ToShortDateString());

                                        if (APD_IBG_BeginDate != Convert.ToDateTime(SA.APD_IBG_BeginDate.Value.ToShortDateString()))
                                            if (!_message.Contains(sp.APD_IBG_ServicePlanName.Trim()))
                                                _message += sp.APD_IBG_ServicePlanName.Trim() + ", ";
                                    }
                                }
                            }
                    }
            }

            if (_message.Length > 0)
                throw new InvalidPluginExecutionException("Cost Plan cannot be processed since the begin date for certain quarters has changed for the following service plans: " + _message.TrimEnd(new char[] { ',' }));
        }

        private void _CreateServiceAuthorization(XrmServiceContext iBudget, Entity original_entity, APD_IBG_serviceplan s, int _quarter, Guid apd_consumerid, Guid apd_wscid, List<APD_servicecode> allsc)
        {
            APD_IBG_serviceauthorization sa = new APD_IBG_serviceauthorization();

            sa.apd_consumerid = new CrmEntityReference(APD_consumer.EntityLogicalName, apd_consumerid);
            sa.apd_providerid = new CrmEntityReference(APD_provider.EntityLogicalName, s.apd_providerid.Id);
            sa.APD_ibg_MedicaidId = (from p in iBudget.APD_providerSet where p.APD_providerId.Value == s.apd_providerid.Id select new { p.APD_ProviderMedicaidWaiverId }).ToList().FirstOrDefault().APD_ProviderMedicaidWaiverId.ToString();
            sa.apd_fiscalyearid = new CrmEntityReference(APD_fiscalyear.EntityLogicalName, ((EntityReference)original_entity["apd_fiscalyearid"]).Id);
            sa.APD_IBG_QuarterCode = _quarter;

            #region begin date, end date
            switch (_quarter)
            {
                case 1:
                    sa.APD_IBG_BeginDate = s.APD_IBG_JulUnitsBeginDate.HasValue ? s.APD_IBG_JulUnitsBeginDate.Value : (s.APD_IBG_AugUnitsBeginDate.HasValue ? s.APD_IBG_AugUnitsBeginDate.Value : (s.APD_IBG_SepUnitsBeginDate.HasValue ? s.APD_IBG_SepUnitsBeginDate.Value : DateTime.MinValue));
                    sa.APD_IBG_EndDate = s.APD_IBG_SepUnitsEndDate.HasValue ? s.APD_IBG_SepUnitsEndDate.Value : (s.APD_IBG_AugUnitsEndDate.HasValue ? s.APD_IBG_AugUnitsEndDate.Value : (s.APD_IBG_JulUnitsEndDate.HasValue ? s.APD_IBG_JulUnitsEndDate.Value : DateTime.MaxValue));
                    break;

                case 2:
                    sa.APD_IBG_BeginDate = s.APD_IBG_OctUnitsBeginDate.HasValue ? s.APD_IBG_OctUnitsBeginDate.Value : (s.APD_IBG_NovUnitsBeginDate.HasValue ? s.APD_IBG_NovUnitsBeginDate.Value : (s.APD_IBG_DecUnitsBeginDate.HasValue ? s.APD_IBG_DecUnitsBeginDate.Value : DateTime.MinValue));
                    sa.APD_IBG_EndDate = s.APD_IBG_DecUnitsEndDate.HasValue ? s.APD_IBG_DecUnitsEndDate.Value : (s.APD_IBG_NovUnitsEndDate.HasValue ? s.APD_IBG_NovUnitsEndDate.Value : (s.APD_IBG_OctUnitsEndDate.HasValue ? s.APD_IBG_OctUnitsEndDate.Value : DateTime.MaxValue));
                    break;

                case 3:
                    sa.APD_IBG_BeginDate = s.APD_IBG_JanUnitsBeginDate.HasValue ? s.APD_IBG_JanUnitsBeginDate.Value : (s.APD_IBG_FebUnitsBeginDate.HasValue ? s.APD_IBG_FebUnitsBeginDate.Value : (s.APD_IBG_MarUnitsBeginDate.HasValue ? s.APD_IBG_MarUnitsBeginDate.Value : DateTime.MinValue));
                    sa.APD_IBG_EndDate = s.APD_IBG_MarUnitsEndDate.HasValue ? s.APD_IBG_MarUnitsEndDate.Value : (s.APD_IBG_FebUnitsEndDate.HasValue ? s.APD_IBG_FebUnitsEndDate.Value : (s.APD_IBG_JanUnitsEndDate.HasValue ? s.APD_IBG_JanUnitsEndDate.Value : DateTime.MaxValue));
                    break;

                case 4:
                    sa.APD_IBG_BeginDate = s.APD_IBG_AprUnitsBeginDate.HasValue ? s.APD_IBG_AprUnitsBeginDate.Value : (s.APD_IBG_MayUnitsBeginDate.HasValue ? s.APD_IBG_MayUnitsBeginDate.Value : (s.APD_IBG_JunUnitsBeginDate.HasValue ? s.APD_IBG_JunUnitsBeginDate.Value : DateTime.MinValue));
                    sa.APD_IBG_EndDate = s.APD_IBG_JunUnitsEndDate.HasValue ? s.APD_IBG_JunUnitsEndDate.Value : (s.APD_IBG_MayUnitsEndDate.HasValue ? s.APD_IBG_MayUnitsEndDate.Value : (s.APD_IBG_AprUnitsEndDate.HasValue ? s.APD_IBG_AprUnitsEndDate.Value : DateTime.MaxValue));
                    break;
            }
            #endregion

            sa.apd_servicecodeid = new CrmEntityReference(APD_servicecode.EntityLogicalName, s.apd_servicecodeid.Id);
            sa.APD_ibg_ServiceDescription = (from sc in allsc where sc.APD_servicecodeId.Value == s.apd_servicecodeid.Id select new { sc.APD_Description }).ToList().FirstOrDefault().APD_Description;
            sa.apd_procedurecodeid = new CrmEntityReference(APD_procedurecode.EntityLogicalName, s.apd_procedurecodeid.Id);
            sa.apd_unittypeid = new CrmEntityReference(APD_unittype.EntityLogicalName, s.apd_unittypeid.Id);
            sa.apd_servicelevelid = new CrmEntityReference(APD_servicelevel.EntityLogicalName, s.apd_servicelevelid.Id);
            sa.apd_ibg_serviceplanid = new CrmEntityReference(APD_IBG_serviceplan.EntityLogicalName, s.APD_IBG_serviceplanId.Value);
            sa.apd_serviceratioid = new CrmEntityReference(APD_serviceratio.EntityLogicalName, s.apd_serviceratioid.Id);

            #region Quarter Amount
            switch (_quarter)
            {
                case 1:
                    sa.APD_IBG_Amount = (s.APD_ibg_jul_amount.HasValue ? s.APD_ibg_jul_amount.Value : 0.0M) +
                                        (s.APD_ibg_aug_amount.HasValue ? s.APD_ibg_aug_amount.Value : 0.0M) +
                                        (s.APD_ibg_sep_amount.HasValue ? s.APD_ibg_sep_amount.Value : 0.0M);
                    break;

                case 2:
                    sa.APD_IBG_Amount = (s.APD_ibg_oct_amount.HasValue ? s.APD_ibg_oct_amount.Value : 0.0M) +
                                        (s.APD_ibg_nov_amount.HasValue ? s.APD_ibg_nov_amount.Value : 0.0M) +
                                        (s.APD_ibg_dec_amount.HasValue ? s.APD_ibg_dec_amount.Value : 0.0M);
                    break;

                case 3:
                    sa.APD_IBG_Amount = (s.APD_ibg_jan_amount.HasValue ? s.APD_ibg_jan_amount.Value : 0.0M) +
                                        (s.APD_ibg_feb_amount.HasValue ? s.APD_ibg_feb_amount.Value : 0.0M) +
                                        (s.APD_ibg_mar_amount.HasValue ? s.APD_ibg_mar_amount.Value : 0.0M);
                    break;

                case 4:
                    sa.APD_IBG_Amount = (s.APD_ibg_apr_amount.HasValue ? s.APD_ibg_apr_amount.Value : 0.0M) +
                                        (s.APD_ibg_may_amount.HasValue ? s.APD_ibg_may_amount.Value : 0.0M) +
                                        (s.APD_ibg_jun_amount.HasValue ? s.APD_ibg_jun_amount.Value : 0.0M);
                    break;
            }
            #endregion

            #region FMMIS rate
            switch (_quarter)
            {
                case 1:
                    sa.APD_ibg_ServiceRate = (s.APD_ibg_jul_servicerate.HasValue ? s.APD_ibg_jul_servicerate.Value : 0.0M) > (s.APD_ibg_aug_rate.HasValue ? s.APD_ibg_aug_rate.Value : 0.0M) ?
                                            ((s.APD_ibg_jul_servicerate.HasValue ? s.APD_ibg_jul_servicerate.Value : 0.0M) > (s.APD_ibg_sep_rate.HasValue ? s.APD_ibg_sep_rate.Value : 0.0M) ?
                                            (s.APD_ibg_jul_servicerate.HasValue ? s.APD_ibg_jul_servicerate.Value : 0.0M) : (s.APD_ibg_sep_rate.HasValue ? s.APD_ibg_sep_rate.Value : 0.0M)) :
                                            ((s.APD_ibg_aug_rate.HasValue ? s.APD_ibg_aug_rate.Value : 0.0M) > (s.APD_ibg_sep_rate.HasValue ? s.APD_ibg_sep_rate.Value : 0.0M) ?
                                            (s.APD_ibg_aug_rate.HasValue ? s.APD_ibg_aug_rate.Value : 0.0M) : (s.APD_ibg_sep_rate.HasValue ? s.APD_ibg_sep_rate.Value : 0.0M));
                    break;

                case 2:
                    sa.APD_ibg_ServiceRate = (s.APD_ibg_oct_rate.HasValue ? s.APD_ibg_oct_rate.Value : 0.0M) > (s.APD_ibg_nov_rate.HasValue ? s.APD_ibg_nov_rate.Value : 0.0M) ?
                                            ((s.APD_ibg_oct_rate.HasValue ? s.APD_ibg_oct_rate.Value : 0.0M) > (s.APD_ibg_dec_rate.HasValue ? s.APD_ibg_dec_rate.Value : 0.0M) ?
                                            (s.APD_ibg_oct_rate.HasValue ? s.APD_ibg_oct_rate.Value : 0.0M) : (s.APD_ibg_dec_rate.HasValue ? s.APD_ibg_dec_rate.Value : 0.0M)) :
                                            ((s.APD_ibg_nov_rate.HasValue ? s.APD_ibg_nov_rate.Value : 0.0M) > (s.APD_ibg_dec_rate.HasValue ? s.APD_ibg_dec_rate.Value : 0.0M) ?
                                            (s.APD_ibg_nov_rate.HasValue ? s.APD_ibg_nov_rate.Value : 0.0M) : (s.APD_ibg_dec_rate.HasValue ? s.APD_ibg_dec_rate.Value : 0.0M));
                    break;

                case 3:
                    sa.APD_ibg_ServiceRate = (s.APD_ibg_jan_rate.HasValue ? s.APD_ibg_jan_rate.Value : 0.0M) > (s.APD_ibg_feb_rate.HasValue ? s.APD_ibg_feb_rate.Value : 0.0M) ?
                                            ((s.APD_ibg_jan_rate.HasValue ? s.APD_ibg_jan_rate.Value : 0.0M) > (s.APD_ibg_mar_rate.HasValue ? s.APD_ibg_mar_rate.Value : 0.0M) ?
                                            (s.APD_ibg_jan_rate.HasValue ? s.APD_ibg_jan_rate.Value : 0.0M) : (s.APD_ibg_mar_rate.HasValue ? s.APD_ibg_mar_rate.Value : 0.0M)) :
                                            ((s.APD_ibg_feb_rate.HasValue ? s.APD_ibg_feb_rate.Value : 0.0M) > (s.APD_ibg_mar_rate.HasValue ? s.APD_ibg_mar_rate.Value : 0.0M) ?
                                            (s.APD_ibg_feb_rate.HasValue ? s.APD_ibg_feb_rate.Value : 0.0M) : (s.APD_ibg_mar_rate.HasValue ? s.APD_ibg_mar_rate.Value : 0.0M));
                    break;

                case 4:
                    sa.APD_ibg_ServiceRate = (s.APD_ibg_apr_rate.HasValue ? s.APD_ibg_apr_rate.Value : 0.0M) > (s.APD_ibg_may_rate.HasValue ? s.APD_ibg_may_rate.Value : 0.0M) ?
                                             ((s.APD_ibg_apr_rate.HasValue ? s.APD_ibg_apr_rate.Value : 0.0M) > (s.APD_ibg_jun_rate.HasValue ? s.APD_ibg_jun_rate.Value : 0.0M) ?
                                             (s.APD_ibg_apr_rate.HasValue ? s.APD_ibg_apr_rate.Value : 0.0M) : (s.APD_ibg_jun_rate.HasValue ? s.APD_ibg_jun_rate.Value : 0.0M)) :
                                             ((s.APD_ibg_may_rate.HasValue ? s.APD_ibg_may_rate.Value : 0.0M) > (s.APD_ibg_jun_rate.HasValue ? s.APD_ibg_jun_rate.Value : 0.0M) ?
                                             (s.APD_ibg_may_rate.HasValue ? s.APD_ibg_may_rate.Value : 0.0M) : (s.APD_ibg_jun_rate.HasValue ? s.APD_ibg_jun_rate.Value : 0.0M));
                    break;
            }
            #endregion

            #region Total Units
            switch (_quarter)
            {
                case 1:
                    sa.APD_IBG_Units = (s.APD_IBG_JulUnits.HasValue ? s.APD_IBG_JulUnits.Value : 0.0) +
                                        (s.APD_IBG_AugUnits.HasValue ? s.APD_IBG_AugUnits.Value : 0.0) +
                                        (s.APD_IBG_SepUnits.HasValue ? s.APD_IBG_SepUnits.Value : 0.0);
                    break;

                case 2:
                    sa.APD_IBG_Units = (s.APD_IBG_OctUnits.HasValue ? s.APD_IBG_OctUnits.Value : 0.0) +
                                        (s.APD_IBG_NovUnits.HasValue ? s.APD_IBG_NovUnits.Value : 0.0) +
                                        (s.APD_IBG_DecUnits.HasValue ? s.APD_IBG_DecUnits.Value : 0.0);
                    break;

                case 3:
                    sa.APD_IBG_Units = (s.APD_IBG_JanUnits.HasValue ? s.APD_IBG_JanUnits.Value : 0.0) +
                                        (s.APD_IBG_FebUnits.HasValue ? s.APD_IBG_FebUnits.Value : 0.0) +
                                        (s.APD_IBG_MarUnits.HasValue ? s.APD_IBG_MarUnits.Value : 0.0);
                    break;

                case 4:
                    sa.APD_IBG_Units = (s.APD_IBG_AprUnits.HasValue ? s.APD_IBG_AprUnits.Value : 0.0) +
                                        (s.APD_IBG_MayUnits.HasValue ? s.APD_IBG_MayUnits.Value : 0.0) +
                                        (s.APD_IBG_JunUnits.HasValue ? s.APD_IBG_JunUnits.Value : 0.0);
                    break;
            }
            #endregion

            sa.APD_IBG_ApprovedDate = DateTime.Now;

            #region Month Details
            switch (_quarter)
            {
                case 1:
                    #region Month 1 Details
                    if (s.APD_IBG_JulUnits.HasValue)
                    {
                        sa.APD_IBG_Month1BeginDate = s.APD_IBG_JulUnitsBeginDate.Value;
                        sa.APD_IBG_Month1EndDate = s.APD_IBG_JulUnitsEndDate.Value;
                        sa.APD_IBG_Month1Units = s.APD_IBG_JulUnits.Value;
                        if ((EntityReference)s.APD_JulyServiceRateId != null) sa.APD_Month1ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_JulyServiceRateId.Id);
                        sa.APD_Month1Rate = s.APD_ibg_jul_servicerate.Value;
                        sa.APD_Month1Amount = s.APD_ibg_jul_amount.Value;
                    }
                    if (s.APD_IBG_AugUnits.HasValue)
                    {
                        sa.APD_IBG_Month2BeginDate = s.APD_IBG_AugUnitsBeginDate.Value;
                        sa.APD_IBG_Month2EndDate = s.APD_IBG_AugUnitsEndDate.Value;
                        sa.APD_IBG_Month2Units = s.APD_IBG_AugUnits.Value;
                        if ((EntityReference)s.APD_AugustServiceRateId != null) sa.APD_Month2ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_AugustServiceRateId.Id);
                        sa.APD_Month2Rate = s.APD_ibg_aug_rate.Value;
                        sa.APD_Month2Amount = s.APD_ibg_aug_amount.Value;
                    }
                    if (s.APD_IBG_SepUnits.HasValue)
                    {
                        sa.APD_IBG_Month3BeginDate = s.APD_IBG_SepUnitsBeginDate.Value;
                        sa.APD_IBG_Month3EndDate = s.APD_IBG_SepUnitsEndDate.Value;
                        sa.APD_IBG_Month3Units = s.APD_IBG_SepUnits.Value;
                        if ((EntityReference)s.APD_SeptemberServiceRateId != null) sa.APD_Month3ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_SeptemberServiceRateId.Id);
                        sa.APD_Month3Rate = s.APD_ibg_sep_rate.Value;
                        sa.APD_Month3Amount = s.APD_ibg_sep_amount.Value;
                    }
                    #endregion
                    break;
                case 2:
                    #region Month 2 Details
                    if (s.APD_IBG_OctUnits.HasValue)
                    {
                        sa.APD_IBG_Month1BeginDate = s.APD_IBG_OctUnitsBeginDate.Value;
                        sa.APD_IBG_Month1EndDate = s.APD_IBG_OctUnitsEndDate.Value;
                        sa.APD_IBG_Month1Units = s.APD_IBG_OctUnits.Value;
                        if ((EntityReference)s.APD_OctoberServiceRateId != null) sa.APD_Month1ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_OctoberServiceRateId.Id);
                        sa.APD_Month1Rate = s.APD_ibg_oct_rate.Value;
                        sa.APD_Month1Amount = s.APD_ibg_oct_amount.Value;
                    }
                    if (s.APD_IBG_NovUnits.HasValue)
                    {
                        sa.APD_IBG_Month2BeginDate = s.APD_IBG_NovUnitsBeginDate.Value;
                        sa.APD_IBG_Month2EndDate = s.APD_IBG_NovUnitsEndDate.Value;
                        sa.APD_IBG_Month2Units = s.APD_IBG_NovUnits.Value;
                        if ((EntityReference)s.APD_NovemberServiceRateId != null) sa.APD_Month2ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_NovemberServiceRateId.Id);
                        sa.APD_Month2Rate = s.APD_ibg_nov_rate.Value;
                        sa.APD_Month2Amount = s.APD_ibg_nov_amount.Value;
                    }
                    if (s.APD_IBG_DecUnits.HasValue)
                    {
                        sa.APD_IBG_Month3BeginDate = s.APD_IBG_DecUnitsBeginDate.Value;
                        sa.APD_IBG_Month3EndDate = s.APD_IBG_DecUnitsEndDate.Value;
                        sa.APD_IBG_Month3Units = s.APD_IBG_DecUnits.Value;
                        if ((EntityReference)s.APD_DecemberServiceRateId != null) sa.APD_Month3ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_DecemberServiceRateId.Id);
                        sa.APD_Month3Rate = s.APD_ibg_dec_rate.Value;
                        sa.APD_Month3Amount = s.APD_ibg_dec_amount.Value;
                    }
                    #endregion
                    break;
                case 3:
                    #region Month 3 Details
                    if (s.APD_IBG_JanUnits.HasValue)
                    {
                        sa.APD_IBG_Month1BeginDate = s.APD_IBG_JanUnitsBeginDate.Value;
                        sa.APD_IBG_Month1EndDate = s.APD_IBG_JanUnitsEndDate.Value;
                        sa.APD_IBG_Month1Units = s.APD_IBG_JanUnits.Value;
                        if ((EntityReference)s.APD_JanuaryServiceRateId != null) sa.APD_Month1ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_JanuaryServiceRateId.Id);
                        sa.APD_Month1Rate = s.APD_ibg_jan_rate.Value;
                        sa.APD_Month1Amount = s.APD_ibg_jan_amount.Value;
                    }
                    if (s.APD_IBG_FebUnits.HasValue)
                    {
                        sa.APD_IBG_Month2BeginDate = s.APD_IBG_FebUnitsBeginDate.Value;
                        sa.APD_IBG_Month2EndDate = s.APD_IBG_FebUnitsEndDate.Value;
                        sa.APD_IBG_Month2Units = s.APD_IBG_FebUnits.Value;
                        if ((EntityReference)s.APD_FebruaryServiceRateId != null) sa.APD_Month2ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_FebruaryServiceRateId.Id);
                        sa.APD_Month2Rate = s.APD_ibg_feb_rate.Value;
                        sa.APD_Month2Amount = s.APD_ibg_feb_amount.Value;
                    }
                    if (s.APD_IBG_MarUnits.HasValue)
                    {
                        sa.APD_IBG_Month3BeginDate = s.APD_IBG_MarUnitsBeginDate.Value;
                        sa.APD_IBG_Month3EndDate = s.APD_IBG_MarUnitsEndDate.Value;
                        sa.APD_IBG_Month3Units = s.APD_IBG_MarUnits.Value;
                        if ((EntityReference)s.APD_MarchServiceRateId != null) sa.APD_Month3ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_MarchServiceRateId.Id);
                        sa.APD_Month3Rate = s.APD_ibg_mar_rate.Value;
                        sa.APD_Month3Amount = s.APD_ibg_mar_amount.Value;
                    }
                    #endregion
                    break;
                case 4:
                    #region Month 4 Details
                    if (s.APD_IBG_AprUnits.HasValue)
                    {
                        sa.APD_IBG_Month1BeginDate = s.APD_IBG_AprUnitsBeginDate.Value;
                        sa.APD_IBG_Month1EndDate = s.APD_IBG_AprUnitsEndDate.Value;
                        sa.APD_IBG_Month1Units = s.APD_IBG_AprUnits.Value;
                        if ((EntityReference)s.APD_AprilServiceRateId != null) sa.APD_Month1ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_AprilServiceRateId.Id);
                        sa.APD_Month1Rate = s.APD_ibg_apr_rate.Value;
                        sa.APD_Month1Amount = s.APD_ibg_apr_amount.Value;
                    }
                    if (s.APD_IBG_MayUnits.HasValue)
                    {
                        sa.APD_IBG_Month2BeginDate = s.APD_IBG_MayUnitsBeginDate.Value;
                        sa.APD_IBG_Month2EndDate = s.APD_IBG_MayUnitsEndDate.Value;
                        sa.APD_IBG_Month2Units = s.APD_IBG_MayUnits.Value;
                        if ((EntityReference)s.APD_MayServiceRateId != null) sa.APD_Month2ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_MayServiceRateId.Id);
                        sa.APD_Month2Rate = s.APD_ibg_may_rate.Value;
                        sa.APD_Month2Amount = s.APD_ibg_may_amount.Value;
                    }
                    if (s.APD_IBG_JunUnits.HasValue)
                    {
                        sa.APD_IBG_Month3BeginDate = s.APD_IBG_JunUnitsBeginDate.Value;
                        sa.APD_IBG_Month3EndDate = s.APD_IBG_JunUnitsEndDate.Value;
                        sa.APD_IBG_Month3Units = s.APD_IBG_JunUnits.Value;
                        if ((EntityReference)s.APD_JuneServiceRateId != null) sa.APD_Month3ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_JuneServiceRateId.Id);
                        sa.APD_Month3Rate = s.APD_ibg_jun_rate.Value;
                        sa.APD_Month3Amount = s.APD_ibg_jun_amount.Value;
                    }
                    #endregion
                    break;
            }
            #endregion

            #region Status Details
            switch (_quarter)
            {
                case 1:
                    if (s.APD_ibg_SPStatusQ1.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ1.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.New;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Edit;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Cancel:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Canceled;
                                break;
                        }
                        sa.APD_PAStatus = s.APD_ibg_PAStatusQ1.Value;
                    }
                    break;

                case 2:
                    if (s.APD_ibg_SPStatusQ2.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ2.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.New;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Edit;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Cancel:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Canceled;
                                break;
                        }
                        sa.APD_PAStatus = s.APD_ibg_PAStatusQ2.Value;
                    }
                    break;

                case 3:
                    if (s.APD_ibg_SPStatusQ3.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ3.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.New;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Edit;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Cancel:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Canceled;
                                break;
                        }
                        sa.APD_PAStatus = s.APD_ibg_PAStatusQ3.Value;
                    }
                    break;

                case 4:
                    if (s.APD_ibg_SPStatusQ4.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ4.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.New;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Edit;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Cancel:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Canceled;
                                break;
                        }
                        sa.APD_PAStatus = s.APD_ibg_PAStatusQ4.Value;
                    }
                    break;

            }
            #endregion

            sa.OwnerId = new CrmEntityReference(SystemUser.EntityLogicalName, apd_wscid);

            iBudget.AddObject(sa);

            try
            {
                iBudget.SaveChanges();
            }
            catch (SaveChangesException ex)
            {
                throw new InvalidPluginExecutionException("Unable to create the service authorization:" + ex.InnerException.Message);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException(ex.InnerException.ToString());
            }

            Guid _newsaid = sa.GetAttributeValue<Guid>("apd_ibg_serviceauthorizationid");

            #region copy notes
            var notes = (from n in iBudget.AnnotationSet
                         where n.ObjectId.Id == s.APD_IBG_serviceplanId.Value
                         select new { n.NoteText, n.Subject }).ToList();

            foreach (var n in notes)
            {
                var annotation = new Annotation
                {
                    Subject = n.Subject,
                    NoteText = n.NoteText,
                    OwnerId = new CrmEntityReference(SystemUser.EntityLogicalName, apd_wscid),
                    ObjectId = new CrmEntityReference(APD_IBG_serviceauthorization.EntityLogicalName, _newsaid)
                };
                iBudget.AddObject(annotation);
            }
            #endregion

            try
            {
                iBudget.SaveChanges();
            }
            catch (SaveChangesException ex)
            {
                throw new InvalidPluginExecutionException("Unable to copy the notes from the SP to the SA:" + ex.InnerException.Message);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException(ex.InnerException.ToString());
            }
        }

        private void _UpdateServiceAuthorization(XrmServiceContext iBudget, Entity original_entity, APD_IBG_serviceplan s, Guid saguid, int _quarter, Guid apd_consumerid, Guid apd_wscid, IOrganizationService _service, List<APD_servicecode> allsc)
        {
            APD_IBG_serviceauthorization sa = new APD_IBG_serviceauthorization();

            sa.APD_IBG_serviceauthorizationId = saguid;

            #region activate service authorization

            //StateCode = 0
            SetStateRequest setStateRequest = new SetStateRequest()
            {
                EntityMoniker = new EntityReference
                {
                    Id = saguid,
                    LogicalName = APD_IBG_serviceauthorization.EntityLogicalName,
                },
                State = new OptionSetValue(0),
                Status = new OptionSetValue((int)apd_ibg_serviceauthorization_statuscode.New)
            };
            _service.Execute(setStateRequest);

            #endregion

            sa.apd_consumerid = new CrmEntityReference(APD_consumer.EntityLogicalName, apd_consumerid);
            sa.apd_providerid = new CrmEntityReference(APD_provider.EntityLogicalName, s.apd_providerid.Id);
            sa.APD_ibg_MedicaidId = (from p in iBudget.APD_providerSet where p.APD_providerId.Value == s.apd_providerid.Id select new { p.APD_ProviderMedicaidWaiverId }).ToList().FirstOrDefault().APD_ProviderMedicaidWaiverId.ToString();
            sa.apd_fiscalyearid = new CrmEntityReference(APD_fiscalyear.EntityLogicalName, ((EntityReference)original_entity["apd_fiscalyearid"]).Id);
            sa.APD_IBG_QuarterCode = _quarter;

            #region begin date, end date
            switch (_quarter)
            {
                case 1:
                    sa.APD_IBG_BeginDate = s.APD_IBG_JulUnitsBeginDate.HasValue ? s.APD_IBG_JulUnitsBeginDate.Value : (s.APD_IBG_AugUnitsBeginDate.HasValue ? s.APD_IBG_AugUnitsBeginDate.Value : (s.APD_IBG_SepUnitsBeginDate.HasValue ? s.APD_IBG_SepUnitsBeginDate.Value : DateTime.MinValue));
                    sa.APD_IBG_EndDate = s.APD_IBG_SepUnitsEndDate.HasValue ? s.APD_IBG_SepUnitsEndDate.Value : (s.APD_IBG_AugUnitsEndDate.HasValue ? s.APD_IBG_AugUnitsEndDate.Value : (s.APD_IBG_JulUnitsEndDate.HasValue ? s.APD_IBG_JulUnitsEndDate.Value : DateTime.MaxValue));
                    break;

                case 2:
                    sa.APD_IBG_BeginDate = s.APD_IBG_OctUnitsBeginDate.HasValue ? s.APD_IBG_OctUnitsBeginDate.Value : (s.APD_IBG_NovUnitsBeginDate.HasValue ? s.APD_IBG_NovUnitsBeginDate.Value : (s.APD_IBG_DecUnitsBeginDate.HasValue ? s.APD_IBG_DecUnitsBeginDate.Value : DateTime.MinValue));
                    sa.APD_IBG_EndDate = s.APD_IBG_DecUnitsEndDate.HasValue ? s.APD_IBG_DecUnitsEndDate.Value : (s.APD_IBG_NovUnitsEndDate.HasValue ? s.APD_IBG_NovUnitsEndDate.Value : (s.APD_IBG_OctUnitsEndDate.HasValue ? s.APD_IBG_OctUnitsEndDate.Value : DateTime.MaxValue));
                    break;

                case 3:
                    sa.APD_IBG_BeginDate = s.APD_IBG_JanUnitsBeginDate.HasValue ? s.APD_IBG_JanUnitsBeginDate.Value : (s.APD_IBG_FebUnitsBeginDate.HasValue ? s.APD_IBG_FebUnitsBeginDate.Value : (s.APD_IBG_MarUnitsBeginDate.HasValue ? s.APD_IBG_MarUnitsBeginDate.Value : DateTime.MinValue));
                    sa.APD_IBG_EndDate = s.APD_IBG_MarUnitsEndDate.HasValue ? s.APD_IBG_MarUnitsEndDate.Value : (s.APD_IBG_FebUnitsEndDate.HasValue ? s.APD_IBG_FebUnitsEndDate.Value : (s.APD_IBG_JanUnitsEndDate.HasValue ? s.APD_IBG_JanUnitsEndDate.Value : DateTime.MaxValue));
                    break;

                case 4:
                    sa.APD_IBG_BeginDate = s.APD_IBG_AprUnitsBeginDate.HasValue ? s.APD_IBG_AprUnitsBeginDate.Value : (s.APD_IBG_MayUnitsBeginDate.HasValue ? s.APD_IBG_MayUnitsBeginDate.Value : (s.APD_IBG_JunUnitsBeginDate.HasValue ? s.APD_IBG_JunUnitsBeginDate.Value : DateTime.MinValue));
                    sa.APD_IBG_EndDate = s.APD_IBG_JunUnitsEndDate.HasValue ? s.APD_IBG_JunUnitsEndDate.Value : (s.APD_IBG_MayUnitsEndDate.HasValue ? s.APD_IBG_MayUnitsEndDate.Value : (s.APD_IBG_AprUnitsEndDate.HasValue ? s.APD_IBG_AprUnitsEndDate.Value : DateTime.MaxValue));
                    break;
            }
            #endregion

            sa.apd_servicecodeid = new CrmEntityReference(APD_servicecode.EntityLogicalName, s.apd_servicecodeid.Id);
            sa.APD_ibg_ServiceDescription = (from sc in allsc where sc.APD_servicecodeId.Value == s.apd_servicecodeid.Id select new { sc.APD_Description }).ToList().FirstOrDefault().APD_Description;
            sa.apd_procedurecodeid = new CrmEntityReference(APD_procedurecode.EntityLogicalName, s.apd_procedurecodeid.Id);
            sa.apd_unittypeid = new CrmEntityReference(APD_unittype.EntityLogicalName, s.apd_unittypeid.Id);
            sa.apd_servicelevelid = new CrmEntityReference(APD_servicelevel.EntityLogicalName, s.apd_servicelevelid.Id);
            sa.apd_ibg_serviceplanid = new CrmEntityReference(APD_IBG_serviceplan.EntityLogicalName, s.APD_IBG_serviceplanId.Value);
            sa.apd_serviceratioid = new CrmEntityReference(APD_serviceratio.EntityLogicalName, s.apd_serviceratioid.Id);

            #region Quarter Amount
            switch (_quarter)
            {
                case 1:
                    sa.APD_IBG_Amount = (s.APD_ibg_jul_amount.HasValue ? s.APD_ibg_jul_amount.Value : 0.0M) +
                                        (s.APD_ibg_aug_amount.HasValue ? s.APD_ibg_aug_amount.Value : 0.0M) +
                                        (s.APD_ibg_sep_amount.HasValue ? s.APD_ibg_sep_amount.Value : 0.0M);
                    break;

                case 2:
                    sa.APD_IBG_Amount = (s.APD_ibg_oct_amount.HasValue ? s.APD_ibg_oct_amount.Value : 0.0M) +
                                        (s.APD_ibg_nov_amount.HasValue ? s.APD_ibg_nov_amount.Value : 0.0M) +
                                        (s.APD_ibg_dec_amount.HasValue ? s.APD_ibg_dec_amount.Value : 0.0M);
                    break;

                case 3:
                    sa.APD_IBG_Amount = (s.APD_ibg_jan_amount.HasValue ? s.APD_ibg_jan_amount.Value : 0.0M) +
                                        (s.APD_ibg_feb_amount.HasValue ? s.APD_ibg_feb_amount.Value : 0.0M) +
                                        (s.APD_ibg_mar_amount.HasValue ? s.APD_ibg_mar_amount.Value : 0.0M);
                    break;

                case 4:
                    sa.APD_IBG_Amount = (s.APD_ibg_apr_amount.HasValue ? s.APD_ibg_apr_amount.Value : 0.0M) +
                                        (s.APD_ibg_may_amount.HasValue ? s.APD_ibg_may_amount.Value : 0.0M) +
                                        (s.APD_ibg_jun_amount.HasValue ? s.APD_ibg_jun_amount.Value : 0.0M);
                    break;
            }
            #endregion

            #region FMMIS rate
            switch (_quarter)
            {
                case 1:
                    sa.APD_ibg_ServiceRate = (s.APD_ibg_jul_servicerate.HasValue ? s.APD_ibg_jul_servicerate.Value : 0.0M) > (s.APD_ibg_aug_rate.HasValue ? s.APD_ibg_aug_rate.Value : 0.0M) ?
                                            ((s.APD_ibg_jul_servicerate.HasValue ? s.APD_ibg_jul_servicerate.Value : 0.0M) > (s.APD_ibg_sep_rate.HasValue ? s.APD_ibg_sep_rate.Value : 0.0M) ?
                                            (s.APD_ibg_jul_servicerate.HasValue ? s.APD_ibg_jul_servicerate.Value : 0.0M) : (s.APD_ibg_sep_rate.HasValue ? s.APD_ibg_sep_rate.Value : 0.0M)) :
                                            ((s.APD_ibg_aug_rate.HasValue ? s.APD_ibg_aug_rate.Value : 0.0M) > (s.APD_ibg_sep_rate.HasValue ? s.APD_ibg_sep_rate.Value : 0.0M) ?
                                            (s.APD_ibg_aug_rate.HasValue ? s.APD_ibg_aug_rate.Value : 0.0M) : (s.APD_ibg_sep_rate.HasValue ? s.APD_ibg_sep_rate.Value : 0.0M));
                    break;

                case 2:
                    sa.APD_ibg_ServiceRate = (s.APD_ibg_oct_rate.HasValue ? s.APD_ibg_oct_rate.Value : 0.0M) > (s.APD_ibg_nov_rate.HasValue ? s.APD_ibg_nov_rate.Value : 0.0M) ?
                                            ((s.APD_ibg_oct_rate.HasValue ? s.APD_ibg_oct_rate.Value : 0.0M) > (s.APD_ibg_dec_rate.HasValue ? s.APD_ibg_dec_rate.Value : 0.0M) ?
                                            (s.APD_ibg_oct_rate.HasValue ? s.APD_ibg_oct_rate.Value : 0.0M) : (s.APD_ibg_dec_rate.HasValue ? s.APD_ibg_dec_rate.Value : 0.0M)) :
                                            ((s.APD_ibg_nov_rate.HasValue ? s.APD_ibg_nov_rate.Value : 0.0M) > (s.APD_ibg_dec_rate.HasValue ? s.APD_ibg_dec_rate.Value : 0.0M) ?
                                            (s.APD_ibg_nov_rate.HasValue ? s.APD_ibg_nov_rate.Value : 0.0M) : (s.APD_ibg_dec_rate.HasValue ? s.APD_ibg_dec_rate.Value : 0.0M));
                    break;

                case 3:
                    sa.APD_ibg_ServiceRate = (s.APD_ibg_jan_rate.HasValue ? s.APD_ibg_jan_rate.Value : 0.0M) > (s.APD_ibg_feb_rate.HasValue ? s.APD_ibg_feb_rate.Value : 0.0M) ?
                                            ((s.APD_ibg_jan_rate.HasValue ? s.APD_ibg_jan_rate.Value : 0.0M) > (s.APD_ibg_mar_rate.HasValue ? s.APD_ibg_mar_rate.Value : 0.0M) ?
                                            (s.APD_ibg_jan_rate.HasValue ? s.APD_ibg_jan_rate.Value : 0.0M) : (s.APD_ibg_mar_rate.HasValue ? s.APD_ibg_mar_rate.Value : 0.0M)) :
                                            ((s.APD_ibg_feb_rate.HasValue ? s.APD_ibg_feb_rate.Value : 0.0M) > (s.APD_ibg_mar_rate.HasValue ? s.APD_ibg_mar_rate.Value : 0.0M) ?
                                            (s.APD_ibg_feb_rate.HasValue ? s.APD_ibg_feb_rate.Value : 0.0M) : (s.APD_ibg_mar_rate.HasValue ? s.APD_ibg_mar_rate.Value : 0.0M));
                    break;

                case 4:
                    sa.APD_ibg_ServiceRate = (s.APD_ibg_apr_rate.HasValue ? s.APD_ibg_apr_rate.Value : 0.0M) > (s.APD_ibg_may_rate.HasValue ? s.APD_ibg_may_rate.Value : 0.0M) ?
                                             ((s.APD_ibg_apr_rate.HasValue ? s.APD_ibg_apr_rate.Value : 0.0M) > (s.APD_ibg_jun_rate.HasValue ? s.APD_ibg_jun_rate.Value : 0.0M) ?
                                             (s.APD_ibg_apr_rate.HasValue ? s.APD_ibg_apr_rate.Value : 0.0M) : (s.APD_ibg_jun_rate.HasValue ? s.APD_ibg_jun_rate.Value : 0.0M)) :
                                             ((s.APD_ibg_may_rate.HasValue ? s.APD_ibg_may_rate.Value : 0.0M) > (s.APD_ibg_jun_rate.HasValue ? s.APD_ibg_jun_rate.Value : 0.0M) ?
                                             (s.APD_ibg_may_rate.HasValue ? s.APD_ibg_may_rate.Value : 0.0M) : (s.APD_ibg_jun_rate.HasValue ? s.APD_ibg_jun_rate.Value : 0.0M));
                    break;
            }
            #endregion

            #region Total Units
            switch (_quarter)
            {
                case 1:
                    sa.APD_IBG_Units = (s.APD_IBG_JulUnits.HasValue ? s.APD_IBG_JulUnits.Value : 0.0) +
                                        (s.APD_IBG_AugUnits.HasValue ? s.APD_IBG_AugUnits.Value : 0.0) +
                                        (s.APD_IBG_SepUnits.HasValue ? s.APD_IBG_SepUnits.Value : 0.0);
                    break;

                case 2:
                    sa.APD_IBG_Units = (s.APD_IBG_OctUnits.HasValue ? s.APD_IBG_OctUnits.Value : 0.0) +
                                        (s.APD_IBG_NovUnits.HasValue ? s.APD_IBG_NovUnits.Value : 0.0) +
                                        (s.APD_IBG_DecUnits.HasValue ? s.APD_IBG_DecUnits.Value : 0.0);
                    break;

                case 3:
                    sa.APD_IBG_Units = (s.APD_IBG_JanUnits.HasValue ? s.APD_IBG_JanUnits.Value : 0.0) +
                                        (s.APD_IBG_FebUnits.HasValue ? s.APD_IBG_FebUnits.Value : 0.0) +
                                        (s.APD_IBG_MarUnits.HasValue ? s.APD_IBG_MarUnits.Value : 0.0);
                    break;

                case 4:
                    sa.APD_IBG_Units = (s.APD_IBG_AprUnits.HasValue ? s.APD_IBG_AprUnits.Value : 0.0) +
                                        (s.APD_IBG_MayUnits.HasValue ? s.APD_IBG_MayUnits.Value : 0.0) +
                                        (s.APD_IBG_JunUnits.HasValue ? s.APD_IBG_JunUnits.Value : 0.0);
                    break;
            }
            #endregion

            #region Month Details
            switch (_quarter)
            {
                case 1:
                    #region Quarter 1 Details
                    if (s.APD_IBG_JulUnits.HasValue)
                    {
                        sa.APD_IBG_Month1BeginDate = s.APD_IBG_JulUnitsBeginDate.Value;
                        sa.APD_IBG_Month1EndDate = s.APD_IBG_JulUnitsEndDate.Value;
                        sa.APD_IBG_Month1Units = s.APD_IBG_JulUnits.Value;
                        if ((EntityReference)s.APD_JulyServiceRateId != null) sa.APD_Month1ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_JulyServiceRateId.Id);
                        sa.APD_Month1Rate = s.APD_ibg_jul_servicerate.Value;
                        sa.APD_Month1Amount = s.APD_ibg_jul_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month1begindate", null);
                        sa.SetAttributeValue("apd_ibg_month1enddate", null);
                        sa.SetAttributeValue("apd_ibg_month1units", null);
                        sa.SetAttributeValue("apd_month1servicerateid", null);
                        sa.SetAttributeValue("apd_month1rate", null);
                        sa.SetAttributeValue("apd_month1amount", null);
                    }

                    if (s.APD_IBG_AugUnits.HasValue)
                    {
                        sa.APD_IBG_Month2BeginDate = s.APD_IBG_AugUnitsBeginDate.Value;
                        sa.APD_IBG_Month2EndDate = s.APD_IBG_AugUnitsEndDate.Value;
                        sa.APD_IBG_Month2Units = s.APD_IBG_AugUnits.Value;
                        if ((EntityReference)s.APD_AugustServiceRateId != null) sa.APD_Month2ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_AugustServiceRateId.Id);
                        sa.APD_Month2Rate = s.APD_ibg_aug_rate.Value;
                        sa.APD_Month2Amount = s.APD_ibg_aug_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month2begindate", null);
                        sa.SetAttributeValue("apd_ibg_month2enddate", null);
                        sa.SetAttributeValue("apd_ibg_month2units", null);
                        sa.SetAttributeValue("apd_month2servicerateid", null);
                        sa.SetAttributeValue("apd_month2rate", null);
                        sa.SetAttributeValue("apd_month2amount", null);
                    }

                    if (s.APD_IBG_SepUnits.HasValue)
                    {
                        sa.APD_IBG_Month3BeginDate = s.APD_IBG_SepUnitsBeginDate.Value;
                        sa.APD_IBG_Month3EndDate = s.APD_IBG_SepUnitsEndDate.Value;
                        sa.APD_IBG_Month3Units = s.APD_IBG_SepUnits.Value;
                        if ((EntityReference)s.APD_SeptemberServiceRateId != null) sa.APD_Month3ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_SeptemberServiceRateId.Id);
                        sa.APD_Month3Rate = s.APD_ibg_sep_rate.Value;
                        sa.APD_Month3Amount = s.APD_ibg_sep_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month3begindate", null);
                        sa.SetAttributeValue("apd_ibg_month3enddate", null);
                        sa.SetAttributeValue("apd_ibg_month3units", null);
                        sa.SetAttributeValue("apd_month3servicerateid", null);
                        sa.SetAttributeValue("apd_month3rate", null);
                        sa.SetAttributeValue("apd_month3amount", null);
                    }
                    #endregion
                    break;
                case 2:
                    #region Quarter 2 Details
                    if (s.APD_IBG_OctUnits.HasValue)
                    {
                        sa.APD_IBG_Month1BeginDate = s.APD_IBG_OctUnitsBeginDate.Value;
                        sa.APD_IBG_Month1EndDate = s.APD_IBG_OctUnitsEndDate.Value;
                        sa.APD_IBG_Month1Units = s.APD_IBG_OctUnits.Value;
                        if ((EntityReference)s.APD_OctoberServiceRateId != null) sa.APD_Month1ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_OctoberServiceRateId.Id);
                        sa.APD_Month1Rate = s.APD_ibg_oct_rate.Value;
                        sa.APD_Month1Amount = s.APD_ibg_oct_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month1begindate", null);
                        sa.SetAttributeValue("apd_ibg_month1enddate", null);
                        sa.SetAttributeValue("apd_ibg_month1units", null);
                        sa.SetAttributeValue("apd_month1servicerateid", null);
                        sa.SetAttributeValue("apd_month1rate", null);
                        sa.SetAttributeValue("apd_month1amount", null);
                    }

                    if (s.APD_IBG_NovUnits.HasValue)
                    {
                        sa.APD_IBG_Month2BeginDate = s.APD_IBG_NovUnitsBeginDate.Value;
                        sa.APD_IBG_Month2EndDate = s.APD_IBG_NovUnitsEndDate.Value;
                        sa.APD_IBG_Month2Units = s.APD_IBG_NovUnits.Value;
                        if ((EntityReference)s.APD_NovemberServiceRateId != null) sa.APD_Month2ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_NovemberServiceRateId.Id);
                        sa.APD_Month2Rate = s.APD_ibg_nov_rate.Value;
                        sa.APD_Month2Amount = s.APD_ibg_nov_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month2begindate", null);
                        sa.SetAttributeValue("apd_ibg_month2enddate", null);
                        sa.SetAttributeValue("apd_ibg_month2units", null);
                        sa.SetAttributeValue("apd_month2servicerateid", null);
                        sa.SetAttributeValue("apd_month2rate", null);
                        sa.SetAttributeValue("apd_month2amount", null);
                    }

                    if (s.APD_IBG_DecUnits.HasValue)
                    {
                        sa.APD_IBG_Month3BeginDate = s.APD_IBG_DecUnitsBeginDate.Value;
                        sa.APD_IBG_Month3EndDate = s.APD_IBG_DecUnitsEndDate.Value;
                        sa.APD_IBG_Month3Units = s.APD_IBG_DecUnits.Value;
                        if ((EntityReference)s.APD_DecemberServiceRateId != null) sa.APD_Month3ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_DecemberServiceRateId.Id);
                        sa.APD_Month3Rate = s.APD_ibg_dec_rate.Value;
                        sa.APD_Month3Amount = s.APD_ibg_dec_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month3begindate", null);
                        sa.SetAttributeValue("apd_ibg_month3enddate", null);
                        sa.SetAttributeValue("apd_ibg_month3units", null);
                        sa.SetAttributeValue("apd_month3servicerateid", null);
                        sa.SetAttributeValue("apd_month3rate", null);
                        sa.SetAttributeValue("apd_month3amount", null);
                    }
                    #endregion
                    break;
                case 3:
                    #region Quarter 3 Details
                    if (s.APD_IBG_JanUnits.HasValue)
                    {
                        sa.APD_IBG_Month1BeginDate = s.APD_IBG_JanUnitsBeginDate.Value;
                        sa.APD_IBG_Month1EndDate = s.APD_IBG_JanUnitsEndDate.Value;
                        sa.APD_IBG_Month1Units = s.APD_IBG_JanUnits.Value;
                        if ((EntityReference)s.APD_JanuaryServiceRateId != null) sa.APD_Month1ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_JanuaryServiceRateId.Id);
                        sa.APD_Month1Rate = s.APD_ibg_jan_rate.Value;
                        sa.APD_Month1Amount = s.APD_ibg_jan_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month1begindate", null);
                        sa.SetAttributeValue("apd_ibg_month1enddate", null);
                        sa.SetAttributeValue("apd_ibg_month1units", null);
                        sa.SetAttributeValue("apd_month1servicerateid", null);
                        sa.SetAttributeValue("apd_month1rate", null);
                        sa.SetAttributeValue("apd_month1amount", null);
                    }

                    if (s.APD_IBG_FebUnits.HasValue)
                    {
                        sa.APD_IBG_Month2BeginDate = s.APD_IBG_FebUnitsBeginDate.Value;
                        sa.APD_IBG_Month2EndDate = s.APD_IBG_FebUnitsEndDate.Value;
                        sa.APD_IBG_Month2Units = s.APD_IBG_FebUnits.Value;
                        if ((EntityReference)s.APD_FebruaryServiceRateId != null) sa.APD_Month2ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_FebruaryServiceRateId.Id);
                        sa.APD_Month2Rate = s.APD_ibg_feb_rate.Value;
                        sa.APD_Month2Amount = s.APD_ibg_feb_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month2begindate", null);
                        sa.SetAttributeValue("apd_ibg_month2enddate", null);
                        sa.SetAttributeValue("apd_ibg_month2units", null);
                        sa.SetAttributeValue("apd_month2servicerateid", null);
                        sa.SetAttributeValue("apd_month2rate", null);
                        sa.SetAttributeValue("apd_month2amount", null);
                    }

                    if (s.APD_IBG_MarUnits.HasValue)
                    {
                        sa.APD_IBG_Month3BeginDate = s.APD_IBG_MarUnitsBeginDate.Value;
                        sa.APD_IBG_Month3EndDate = s.APD_IBG_MarUnitsEndDate.Value;
                        sa.APD_IBG_Month3Units = s.APD_IBG_MarUnits.Value;
                        if ((EntityReference)s.APD_MarchServiceRateId != null) sa.APD_Month3ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_MarchServiceRateId.Id);
                        sa.APD_Month3Rate = s.APD_ibg_mar_rate.Value;
                        sa.APD_Month3Amount = s.APD_ibg_mar_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month3begindate", null);
                        sa.SetAttributeValue("apd_ibg_month3enddate", null);
                        sa.SetAttributeValue("apd_ibg_month3units", null);
                        sa.SetAttributeValue("apd_month3servicerateid", null);
                        sa.SetAttributeValue("apd_month3rate", null);
                        sa.SetAttributeValue("apd_month3amount", null);
                    }
                    #endregion
                    break;
                case 4:
                    #region Quarter 4 Details
                    if (s.APD_IBG_AprUnits.HasValue)
                    {
                        sa.APD_IBG_Month1BeginDate = s.APD_IBG_AprUnitsBeginDate.Value;
                        sa.APD_IBG_Month1EndDate = s.APD_IBG_AprUnitsEndDate.Value;
                        sa.APD_IBG_Month1Units = s.APD_IBG_AprUnits.Value;
                        if ((EntityReference)s.APD_AprilServiceRateId != null) sa.APD_Month1ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_AprilServiceRateId.Id);
                        sa.APD_Month1Rate = s.APD_ibg_apr_rate.Value;
                        sa.APD_Month1Amount = s.APD_ibg_apr_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month1begindate", null);
                        sa.SetAttributeValue("apd_ibg_month1enddate", null);
                        sa.SetAttributeValue("apd_ibg_month1units", null);
                        sa.SetAttributeValue("apd_month1servicerateid", null);
                        sa.SetAttributeValue("apd_month1rate", null);
                        sa.SetAttributeValue("apd_month1amount", null);
                    }

                    if (s.APD_IBG_MayUnits.HasValue)
                    {
                        sa.APD_IBG_Month2BeginDate = s.APD_IBG_MayUnitsBeginDate.Value;
                        sa.APD_IBG_Month2EndDate = s.APD_IBG_MayUnitsEndDate.Value;
                        sa.APD_IBG_Month2Units = s.APD_IBG_MayUnits.Value;
                        if ((EntityReference)s.APD_MayServiceRateId != null) sa.APD_Month2ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_MayServiceRateId.Id);
                        sa.APD_Month2Rate = s.APD_ibg_may_rate.Value;
                        sa.APD_Month2Amount = s.APD_ibg_may_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month2begindate", null);
                        sa.SetAttributeValue("apd_ibg_month2enddate", null);
                        sa.SetAttributeValue("apd_ibg_month2units", null);
                        sa.SetAttributeValue("apd_month2servicerateid", null);
                        sa.SetAttributeValue("apd_month2rate", null);
                        sa.SetAttributeValue("apd_month2amount", null);
                    }

                    if (s.APD_IBG_JunUnits.HasValue)
                    {
                        sa.APD_IBG_Month3BeginDate = s.APD_IBG_JunUnitsBeginDate.Value;
                        sa.APD_IBG_Month3EndDate = s.APD_IBG_JunUnitsEndDate.Value;
                        sa.APD_IBG_Month3Units = s.APD_IBG_JunUnits.Value;
                        if ((EntityReference)s.APD_JuneServiceRateId != null) sa.APD_Month3ServiceRateId = new CrmEntityReference(APD_rate.EntityLogicalName, s.APD_JuneServiceRateId.Id);
                        sa.APD_Month3Rate = s.APD_ibg_jun_rate.Value;
                        sa.APD_Month3Amount = s.APD_ibg_jun_amount.Value;
                    }
                    else
                    {
                        sa.SetAttributeValue("apd_ibg_month3begindate", null);
                        sa.SetAttributeValue("apd_ibg_month3enddate", null);
                        sa.SetAttributeValue("apd_ibg_month3units", null);
                        sa.SetAttributeValue("apd_month3servicerateid", null);
                        sa.SetAttributeValue("apd_month3rate", null);
                        sa.SetAttributeValue("apd_month3amount", null);
                    }
                    #endregion
                    break;
            }
            #endregion

            #region Status Details
            switch (_quarter)
            {
                case 1:
                    if (s.APD_ibg_SPStatusQ1.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ1.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.New;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Edit;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Cancel:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Canceled;
                                break;
                            default:
                                break;
                        }
                        sa.APD_PAStatus = s.APD_ibg_PAStatusQ1.Value;
                    }
                    break;

                case 2:
                    if (s.APD_ibg_SPStatusQ2.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ2.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.New;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Edit;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Cancel:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Canceled;
                                break;
                            default:
                                break;
                        }
                        sa.APD_PAStatus = s.APD_ibg_PAStatusQ2.Value;
                    }
                    break;

                case 3:
                    if (s.APD_ibg_SPStatusQ3.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ3.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.New;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Edit;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Cancel:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Canceled;
                                break;
                            default:
                                break;
                        }
                        sa.APD_PAStatus = s.APD_ibg_PAStatusQ3.Value;
                    }
                    break;

                case 4:
                    if (s.APD_ibg_SPStatusQ4.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ4.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.New;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Edit;
                                break;
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Cancel:
                                sa.statuscode = (int)apd_ibg_serviceauthorization_statuscode.Canceled;
                                break;
                            default:
                                break;
                        }
                        sa.APD_PAStatus = s.APD_ibg_PAStatusQ4.Value;
                    }
                    break;

            }
            #endregion

            iBudget.ClearChanges();
            iBudget.Attach(sa);

            iBudget.UpdateObject(sa);

            try
            {
                iBudget.SaveChanges();
            }
            catch (SaveChangesException ex)
            {
                throw new InvalidPluginExecutionException("Unable to update the service authorization:" + ex.InnerException.Message);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException(ex.InnerException.ToString());
            }

            #region Status Deleted
            switch (_quarter)
            {
                case 1:
                    if (s.APD_ibg_SPStatusQ1.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ1.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Deleted:
                                var cols = new ColumnSet(new[] { "statecode", "statuscode" });

                                //Check if it is Active or not
                                var entity = _service.Retrieve(APD_IBG_serviceauthorization.EntityLogicalName, saguid, cols);

                                if (entity != null && entity.GetAttributeValue<OptionSetValue>("statecode").Value == 0)
                                {
                                    //StateCode = 1 and StatusCode = 2 for deactivating Account or Contact
                                    setStateRequest = new SetStateRequest()
                                    {
                                        EntityMoniker = new EntityReference
                                        {
                                            Id = saguid,
                                            LogicalName = APD_IBG_serviceauthorization.EntityLogicalName,
                                        },
                                        State = new OptionSetValue(1),
                                        Status = new OptionSetValue((int)apd_ibg_serviceauthorization_statuscode.Deleted)
                                    };
                                    _service.Execute(setStateRequest);
                                }
                                break;
                        }
                    }
                    break;
                case 2:
                    if (s.APD_ibg_SPStatusQ2.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ2.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Deleted:
                                var cols = new ColumnSet(new[] { "statecode", "statuscode" });

                                //Check if it is Active or not
                                var entity = _service.Retrieve(APD_IBG_serviceauthorization.EntityLogicalName, saguid, cols);

                                if (entity != null && entity.GetAttributeValue<OptionSetValue>("statecode").Value == 0)
                                {
                                    //StateCode = 1 and StatusCode = 2 for deactivating Account or Contact
                                    setStateRequest = new SetStateRequest()
                                    {
                                        EntityMoniker = new EntityReference
                                        {
                                            Id = saguid,
                                            LogicalName = APD_IBG_serviceauthorization.EntityLogicalName,
                                        },
                                        State = new OptionSetValue(1),
                                        Status = new OptionSetValue((int)apd_ibg_serviceauthorization_statuscode.Deleted)
                                    };
                                    _service.Execute(setStateRequest);
                                }
                                break;
                        }
                    }
                    break;
                case 3:
                    if (s.APD_ibg_SPStatusQ3.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ3.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Deleted:
                                var cols = new ColumnSet(new[] { "statecode", "statuscode" });

                                //Check if it is Active or not
                                var entity = _service.Retrieve(APD_IBG_serviceauthorization.EntityLogicalName, saguid, cols);

                                if (entity != null && entity.GetAttributeValue<OptionSetValue>("statecode").Value == 0)
                                {
                                    //StateCode = 1 and StatusCode = 2 for deactivating Account or Contact
                                    setStateRequest = new SetStateRequest()
                                    {
                                        EntityMoniker = new EntityReference
                                        {
                                            Id = saguid,
                                            LogicalName = APD_IBG_serviceauthorization.EntityLogicalName,
                                        },
                                        State = new OptionSetValue(1),
                                        Status = new OptionSetValue((int)apd_ibg_serviceauthorization_statuscode.Deleted)
                                    };
                                    _service.Execute(setStateRequest);
                                }
                                break;
                        }
                    }
                    break;
                case 4:
                    if (s.APD_ibg_SPStatusQ4.HasValue)
                    {
                        switch (s.APD_ibg_SPStatusQ4.Value)
                        {
                            case (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Deleted:
                                var cols = new ColumnSet(new[] { "statecode", "statuscode" });

                                //Check if it is Active or not
                                var entity = _service.Retrieve(APD_IBG_serviceauthorization.EntityLogicalName, saguid, cols);

                                if (entity != null && entity.GetAttributeValue<OptionSetValue>("statecode").Value == 0)
                                {
                                    //StateCode = 1 and StatusCode = 2 for deactivating Account or Contact
                                    setStateRequest = new SetStateRequest()
                                    {
                                        EntityMoniker = new EntityReference
                                        {
                                            Id = saguid,
                                            LogicalName = APD_IBG_serviceauthorization.EntityLogicalName,
                                        },
                                        State = new OptionSetValue(1),
                                        Status = new OptionSetValue((int)apd_ibg_serviceauthorization_statuscode.Deleted)
                                    };
                                    _service.Execute(setStateRequest);
                                }
                                break;
                        }
                    }
                    break;
            }
            #endregion

            #region delete existing notes

            var existingnotes = (from n in iBudget.AnnotationSet
                                 where n.ObjectId.Id == saguid
                                 select n).ToList();

            foreach (var n in existingnotes)
            {
                iBudget.DeleteObject(n);
            }
            #endregion

            #region copy notes

            var notes = (from n in iBudget.AnnotationSet
                         where n.ObjectId.Id == s.APD_IBG_serviceplanId.Value
                         select new { n.NoteText, n.Subject }).ToList();

            foreach (var n in notes)
            {
                var annotation = new Annotation
                {
                    Subject = n.Subject,
                    NoteText = n.NoteText,
                    OwnerId = new CrmEntityReference(SystemUser.EntityLogicalName, apd_wscid),
                    ObjectId = new CrmEntityReference(APD_IBG_serviceauthorization.EntityLogicalName, saguid)
                };
                iBudget.AddObject(annotation);
            }
            #endregion

            try
            {
                iBudget.SaveChanges();
            }
            catch (SaveChangesException ex)
            {
                throw new InvalidPluginExecutionException("Unable to update the service authorization:" + ex.InnerException.Message);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException(ex.InnerException.ToString());
            }
        }

        private void _ReturnBeginEndDates(APD_IBG_serviceplan sp, out bool Q1exists, out bool Q2exists, out bool Q3exists, out bool Q4exists)
        {
            Q1exists = Q2exists = Q3exists = Q4exists = false;

            if (sp.APD_IBG_JulUnits.HasValue
                || sp.APD_IBG_AugUnits.HasValue
                || sp.APD_IBG_SepUnits.HasValue)
                Q1exists = true;

            if (sp.APD_ibg_PAStatusQ1.HasValue)
                if (sp.APD_ibg_PAStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ1.Rejected)
                    throw new InvalidPluginExecutionException("Cost Plan cannot be processed since Quarter 1 from " + sp.APD_IBG_ServicePlanName + " is in Rejected Status.");

            if (sp.APD_IBG_OctUnits.HasValue
                || sp.APD_IBG_NovUnits.HasValue
                || sp.APD_IBG_DecUnits.HasValue)
                Q2exists = true;

            if (sp.APD_ibg_PAStatusQ2.HasValue)
                if (sp.APD_ibg_PAStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ2.Rejected)
                    throw new InvalidPluginExecutionException("Cost Plan cannot be processed since Quarter 2 from " + sp.APD_IBG_ServicePlanName + " is in Rejected Status.");

            if (sp.APD_IBG_JanUnits.HasValue
                || sp.APD_IBG_FebUnits.HasValue
                || sp.APD_IBG_MarUnits.HasValue)
                Q3exists = true;

            if (sp.APD_ibg_PAStatusQ3.HasValue)
                if (sp.APD_ibg_PAStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ3.Rejected)
                    throw new InvalidPluginExecutionException("Cost Plan cannot be processed since Quarter 2 from " + sp.APD_IBG_ServicePlanName + " is in Rejected Status.");

            if (sp.APD_IBG_AprUnits.HasValue
                || sp.APD_IBG_MayUnits.HasValue
                || sp.APD_IBG_JunUnits.HasValue)
                Q4exists = true;

            if (sp.APD_ibg_PAStatusQ4.HasValue)
                if (sp.APD_ibg_PAStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ4.Rejected)
                    throw new InvalidPluginExecutionException("Cost Plan cannot be processed since Quarter 2 from " + sp.APD_IBG_ServicePlanName + " is in Rejected Status.");
        }

        private void Validate_4421(XrmServiceContext iBudget, Entity original_entity, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc)
        {
            #region get service code id for service code name = 4421

            var sc_4421 = (from s in allsc
                           where s.APD_ServiceCodeName == "4421"
                           select new { s.APD_servicecodeId, s.APD_Description }).ToList().FirstOrDefault();

            #endregion

            #region get service plans for service code = 4421

            var sp_4421 = (from s in allsps
                           where s.apd_annualcostplanid.Id == original_entity.Id
                           && s.apd_servicecodeid.Id == sc_4421.APD_servicecodeId.Value
                           select s).ToList();

            #endregion

            #region check for valid PA#, Units in Approved Service

            var cap = (from c in iBudget.APD_IBG_consumerapprovedserviceSet
                       where c.apd_consumerid.Id == ((EntityReference)original_entity["apd_consumerid"]).Id
                       && c.apd_servicecodeid.Id == sc_4421.APD_servicecodeId.Value
                       && c.statecode == 0
                       && (c.APD_PriorAuthorizationStatus == "New" || c.APD_PriorAuthorizationStatus == "Edit")
                       select c).ToList();

            foreach (var sp in sp_4421)
            {
                var caps = cap.FindAll(q => Convert.ToDateTime(q.APD_IBG_BeginDate.Value.ToShortDateString()) <= Convert.ToDateTime(sp.APD_ibg_ServicePlanBeginDate.Value.ToShortDateString())
                            && Convert.ToDateTime(q.APD_IBG_EndDate.Value.ToShortDateString()) >= Convert.ToDateTime(sp.APD_ibg_ServicePlanEndDate.Value.ToShortDateString()));


                if (caps.Count == 0)
                    throw new InvalidPluginExecutionException("The Service Plan for CDC PCA<21 (4421) does not fall within the date range of an existing approved service Prior Authorization from EQ Health.");
                else
                {
                    if (sp.APD_IBG_TotalNumberOfUnits.Value > Convert.ToDouble(caps.FirstOrDefault().APD_PriorAuthorizationUnits))
                    {
                        throw new InvalidPluginExecutionException("This consumers approved service prior authorization for PCA<21 (4421) is " + caps.FirstOrDefault().APD_PriorAuthorizationUnits.ToString() + " units." + Environment.NewLine +
                            "This service plan units " + sp.APD_IBG_TotalNumberOfUnits.ToString() + " exceeds the authorization limits." + Environment.NewLine +
                            " Please change the service plan so units are within those of the prior authorization.");
                    }
                }
            }
            #endregion
        }

        private void Validate_Rules(XrmServiceContext iBudget, Entity original_entity, string FY, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc)
        {
            Validate_Units(new string[] { "4080", "4081", "4082", "4083", "4084", "4085", "4086", "4087" }, Convert.ToDouble(112), iBudget, original_entity, "week", FY, allsps, allsc);

            Validate_Units(new string[] { "4231", "4232" }, Convert.ToDouble(32), iBudget, original_entity, "day", FY, allsps, allsc);

            Validate_Units(new string[] { "4161", "4162" }, Convert.ToDouble(96), iBudget, original_entity, "day", FY, allsps, allsc);

            Validate_Units(new string[] { "4201", "4202" }, Convert.ToDouble(96), iBudget, original_entity, "day", FY, allsps, allsc);

            Validate_Procedure_Code_Rules(iBudget, original_entity, allsps);

            Validate_Procedure_Code_Units(new string[] { "T4521UC", "T4522UC", "T4523UC", "T4524UC", "T4525UC", "T4526UC", "T4527UC", "T4528UC", "T4529UC", "T4530UC", "T4531UC", "T4532UC", "T4533UC", "T4534UC", "T4535UC", "T4543UC" }, Convert.ToDouble(200), iBudget, original_entity, "month", allsps);
        }

        private void Validate_Procedure_Code_Units(string[] _procedureCode, double _maxUnits, XrmServiceContext iBudget, Entity original_entity, string _duration, List<APD_IBG_serviceplan> allsps)
        {
            Guid _pAnnualCostPlanId = original_entity.Id;
            string _servicecode_nameordescription = "";

            #region Get Service Code Id, Service Description for Service Code assuming Service Code will be unique

            if (!string.IsNullOrEmpty(_procedureCode.ToString().Trim()))
            {
                List<APD_procedurecode> procedures = new List<APD_procedurecode>();

                foreach (var item in _procedureCode)
                {
                    APD_procedurecode temp = (from c in iBudget.APD_procedurecodeSet
                                              where c.APD_ProcedureCodeName == item.ToString()
                                              select c).ToList().FirstOrDefault();

                    if (temp != null)
                        procedures.Add(temp);

                    temp = null;
                }

                if (procedures != null)
                    if (procedures.Count > 0)
                    {
                        #region Get Service Plans for Annual Cost Plan Id & Service Code Id

                        List<APD_IBG_serviceplan> serviceplans = new List<APD_IBG_serviceplan>();

                        foreach (var svc in procedures)
                        {
                            List<APD_IBG_serviceplan> temp = (from c in allsps
                                                              where c.apd_annualcostplanid.Id == _pAnnualCostPlanId
                                                                    && c.apd_procedurecodeid.Id == svc.APD_procedurecodeId.Value
                                                              //&& c.APD_IBG_TotalAmount.Value > 0
                                                              select c).ToList();
                            if (temp != null)
                                serviceplans.AddRange(temp);

                            temp = null;
                            _servicecode_nameordescription += svc.APD_Description.ToString() + Environment.NewLine;
                        }

                        #region Check Total Number of Units for each Service Plan & Max Units supplied

                        double jul_total = 0, aug_total = 0, sep_total = 0, oct_total = 0, nov_total = 0, dec_total = 0, jan_total = 0, feb_total = 0, mar_total = 0, apr_total = 0, may_total = 0, jun_total = 0;

                        // Calculate the totals of the Service Plans found
                        foreach (var sp in serviceplans)
                        {
                            if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                if ((sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                    sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit) ||
                                    (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Cancel &&
                                    sp.APD_ibg_PAStatusQ1.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ1.Approved))
                                {
                                    jul_total += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                    aug_total += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                    sep_total += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                }

                            if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                if ((sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                    sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit) ||
                                    (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Cancel &&
                                    sp.APD_ibg_PAStatusQ2.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ2.Approved))
                                {
                                    oct_total += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                    nov_total += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                    dec_total += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;

                                }

                            if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                if ((sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                    sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit) ||
                                    (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Cancel &&
                                    sp.APD_ibg_PAStatusQ3.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ3.Approved))
                                {
                                    jan_total += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                    feb_total += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                    mar_total += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                }

                            if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                if ((sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                    sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit) ||
                                    (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Cancel &&
                                    sp.APD_ibg_PAStatusQ4.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ4.Approved))
                                {
                                    apr_total += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                    may_total += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                    jun_total += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                }

                        }

                        if (jul_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + jul_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of July exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (aug_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + aug_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of August exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (sep_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + sep_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of September exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (oct_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + oct_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of October exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (nov_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + nov_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of November exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (dec_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + dec_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of December exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + Environment.NewLine + ".Please modify the service plan(s) to reflect this limit.");
                        }
                        if (jan_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + jan_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of January exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (feb_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + feb_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of February exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (mar_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + mar_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of March exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (apr_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + apr_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of April exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (may_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + may_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of May exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (jun_total > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + jun_total.ToString("N0") + " for the following Service(s): " + Environment.NewLine + _servicecode_nameordescription.ToString() + "in this cost plan for the month of June exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }

                        #endregion


                        #endregion
                    }
            }
            #endregion
        }

        protected void Validate_Units(string[] _serviceCode, double _maxUnits, XrmServiceContext iBudget, Entity original_entity, string _duration, string FY, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc)
        {
            Guid _pAnnualCostPlanId = original_entity.Id;

            #region Get Service Code Id, Service Description for Service Code assuming Service Code will be unique

            if (!string.IsNullOrEmpty(_serviceCode.ToString().Trim()))
            {
                List<APD_servicecode> services = new List<APD_servicecode>();

                foreach (var item in _serviceCode)
                {
                    APD_servicecode temp = (from c in allsc
                                            where c.APD_ServiceCodeName == item.ToString()
                                            select c).ToList().FirstOrDefault();

                    if (temp != null)
                        services.Add(temp);
                }

                if (services != null)
                    if (services.Count > 0)
                    {
                        #region Get Service Plans for Annual Cost Plan Id & Service Code Id

                        var servicefamily = (from c in iBudget.APD_servicefamilySet
                                             select new { c.APD_servicefamilyname, c.APD_servicefamilyId }).ToList();

                        string _servicecode_nameordescription = "";

                        List<APD_IBG_serviceplan> serviceplans = new List<APD_IBG_serviceplan>();

                        foreach (var svc in services)
                        {
                            if (svc.APD_Description != null)
                            {
                                _servicecode_nameordescription += svc.APD_Description + ", ";
                            }

                            APD_IBG_serviceplan temp = (from c in allsps
                                                        where c.apd_annualcostplanid.Id == _pAnnualCostPlanId &&
                                                              c.apd_servicecodeid.Id == svc.APD_servicecodeId
                                                        select c).FirstOrDefault();
                            if (temp != null)
                                serviceplans.Add(temp);
                        }

                        if (_serviceCode.Length < 3)
                            _servicecode_nameordescription = "(s): " + _servicecode_nameordescription.Trim().TrimEnd(new char[] { ',' });
                        else
                            _servicecode_nameordescription = " Family: " + (from s in servicefamily where s.APD_servicefamilyId.Value == services[0].apd_servicefamilyid.Id select new { s.APD_servicefamilyname }).ToList().FirstOrDefault().APD_servicefamilyname;

                        #region Check Total Number of Units for each Service Plan & Max Units supplied

                        double jul_total = 0, aug_total = 0, sep_total = 0, oct_total = 0, nov_total = 0, dec_total = 0, jan_total = 0, feb_total = 0, mar_total = 0, apr_total = 0, may_total = 0, jun_total = 0;
                        double jul_total_asis = 0, aug_total_asis = 0, sep_total_asis = 0, oct_total_asis = 0, nov_total_asis = 0, dec_total_asis = 0, jan_total_asis = 0, feb_total_asis = 0, mar_total_asis = 0, apr_total_asis = 0, may_total_asis = 0, jun_total_asis = 0;

                        // Calculate the totals of the Service Plans found
                        foreach (var sp in serviceplans)
                        {
                            double _factor_duration = 1;
                            if (_serviceCode.Length > 2)
                            {
                                switch (sp.apd_unittypeid.Name.ToLower())
                                {
                                    case "day":
                                        _factor_duration = 6;
                                        break;
                                    case "hour":
                                        _factor_duration = 1;
                                        break;
                                    case "quarter hour":
                                        _factor_duration = .25;
                                        break;
                                    default:
                                        break;
                                }
                            }

                            if (sp.APD_IBG_TotalNumberOfUnits != 0.0)
                            {

                                if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                    if ((sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                                            sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit) ||
                                                            (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Cancel &&
                                                           sp.APD_ibg_PAStatusQ1.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ1.Approved))
                                    {
                                        jul_total += (sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0) * _factor_duration;
                                        aug_total += (sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0) * _factor_duration;
                                        sep_total += (sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0) * _factor_duration;

                                        jul_total_asis += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                        aug_total_asis += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                        sep_total_asis += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                    if ((sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                                               sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit) ||
                                                               (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Cancel &&
                                                              sp.APD_ibg_PAStatusQ2.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ2.Approved))
                                    {
                                        oct_total += (sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0) * _factor_duration;
                                        nov_total += (sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0) * _factor_duration;
                                        dec_total += (sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0) * _factor_duration;

                                        oct_total_asis += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                        nov_total_asis += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                        dec_total_asis += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                    if ((sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                                            sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit) ||
                                                            (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Cancel &&
                                                           sp.APD_ibg_PAStatusQ3.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ3.Approved))
                                    {
                                        jan_total += (sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0) * _factor_duration;
                                        feb_total += (sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0) * _factor_duration;
                                        mar_total += (sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0) * _factor_duration;

                                        jan_total_asis += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                        feb_total_asis += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                        mar_total_asis += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                    if ((sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                                             sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit) ||
                                                             (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Cancel &&
                                                            sp.APD_ibg_PAStatusQ4.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ4.Approved))
                                    {
                                        apr_total += (sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0) * _factor_duration;
                                        may_total += (sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0) * _factor_duration;
                                        jun_total += (sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0) * _factor_duration;

                                        apr_total_asis += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                        may_total_asis += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                        jun_total_asis += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                    }
                            }
                        }

                        double _factor = 1;

                        // If Total Units > Max Units supplied throw Exception
                        switch (_duration)
                        {
                            case "week":
                                _factor = Math.Round(31 / 7.0, 2);
                                break;
                            case "day":
                                _factor = 31;
                                break;
                            default:
                                _factor = 1;
                                break;
                        }

                        if (jul_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + jul_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of July exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (aug_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + aug_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of August exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (oct_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + oct_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of October exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (dec_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + dec_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of December exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + Environment.NewLine + ".Please modify the service plan(s) to reflect this limit.");
                        }
                        if (jan_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + jan_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of January exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (mar_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + mar_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of March exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (may_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + may_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of May exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }

                        switch (_duration)
                        {
                            case "week":
                                _factor = Math.Round(30 / 7.0, 2);
                                break;
                            case "day":
                                _factor = 30;
                                break;
                            default:
                                _factor = 1;
                                break;
                        }
                        if (sep_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + sep_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of September exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (nov_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + nov_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of November exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (apr_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + apr_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of April exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        if (jun_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + jun_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of June exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }

                        switch (_duration)
                        {
                            case "week":
                                _factor = Math.Round(ReturnMonthStart(2, FY).AddMonths(1).AddDays(-1).Day / 7.0, 2);
                                break;
                            case "day":
                                _factor = ReturnMonthStart(2, FY).AddMonths(1).AddDays(-1).Day;
                                break;
                            default:
                                _factor = 1;
                                break;
                        }
                        if (feb_total / _factor > _maxUnits)
                        {
                            throw new InvalidPluginExecutionException("The total units of " + feb_total_asis.ToString("N0") + " for the following Service" + _servicecode_nameordescription.ToString() + Environment.NewLine + " in this cost plan for the month of February exceeds the maximum allowed units of " + _maxUnits.ToString() + " per " + _duration + "." + Environment.NewLine + "Please modify the service plan(s) to reflect this limit.");
                        }
                        #endregion


                        #endregion
                    }
            }
            #endregion
        }

        private void Validate_Procedure_Code_Rules(XrmServiceContext iBudget, Entity original_entity, List<APD_IBG_serviceplan> allsps)
        {
            var pc_rules = (from pcr in iBudget.APD_procedurecodeserviceruleSet
                            where pcr.statecode.Value == 0
                            select pcr).ToList();

            if (pc_rules.Count() > 0)
            {
                foreach (var pcr in pc_rules)
                {
                    string _servicecode_description = (from p in iBudget.APD_procedurecodeSet
                                                       where p.APD_procedurecodeId.Value == pcr.APD_ProcedureCodeId.Id
                                                       select new { p.APD_Description }).FirstOrDefault().APD_Description;


                    double _maxYearlyUnits = 9999999999;
                    double _maxMonthUnits = 9999999999;

                    double jul_total = 0, aug_total = 0, sep_total = 0, oct_total = 0, nov_total = 0, dec_total = 0,
                                jan_total = 0, feb_total = 0, mar_total = 0, apr_total = 0, may_total = 0, jun_total = 0, apd_ibg_totalnumberofunits = 0;

                    if (pcr.APD_YearlyLimit.ToLower() != "none")
                        _maxYearlyUnits = Convert.ToDouble(pcr.APD_YearlyLimit);

                    if (pcr.APD_MonthlyLimit.ToLower() != "none")
                        _maxMonthUnits = Convert.ToDouble(pcr.APD_MonthlyLimit);

                    if (_maxYearlyUnits != 9999999999 || _maxMonthUnits != 9999999999)
                    {
                        #region Get Service Plans for Annual Cost Plan Id & Service Code Id matching Support Coordination

                        var matchedsps = (from s in allsps
                                          where s.apd_procedurecodeid.Id == pcr.APD_ProcedureCodeId.Id
                                          select s).ToList();

                        #endregion

                        if (matchedsps.Count() > 0)
                        {
                            jul_total = 0; aug_total = 0; sep_total = 0; oct_total = 0; nov_total = 0; dec_total = 0; jan_total = 0; feb_total = 0; mar_total = 0; apr_total = 0; may_total = 0; jun_total = 0; apd_ibg_totalnumberofunits = 0;

                            //Calculate the totals of each of the Service Plans for each month
                            foreach (var s in matchedsps)
                            {
                                if (s.APD_ibg_SPStatusQ1.HasValue && s.APD_ibg_PAStatusQ1.HasValue)
                                    if ((s.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                        s.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit) ||
                                        (s.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Cancel &&
                                        s.APD_ibg_PAStatusQ1.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ1.Approved))
                                    {
                                        jul_total += s.APD_IBG_JulUnits.HasValue ? s.APD_IBG_JulUnits.Value : 0.0;
                                        aug_total += s.APD_IBG_AugUnits.HasValue ? s.APD_IBG_AugUnits.Value : 0.0;
                                        sep_total += s.APD_IBG_SepUnits.HasValue ? s.APD_IBG_SepUnits.Value : 0.0;
                                        apd_ibg_totalnumberofunits += (s.APD_IBG_JulUnits.HasValue ? s.APD_IBG_JulUnits.Value : 0.0) + (s.APD_IBG_AugUnits.HasValue ? s.APD_IBG_AugUnits.Value : 0.0) + (s.APD_IBG_SepUnits.HasValue ? s.APD_IBG_SepUnits.Value : 0.0);

                                    }
                                if (s.APD_ibg_SPStatusQ2.HasValue && s.APD_ibg_PAStatusQ2.HasValue)
                                    if ((s.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                        s.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit) ||
                                        (s.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Cancel &&
                                        s.APD_ibg_PAStatusQ2.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ2.Approved))
                                    {
                                        oct_total += s.APD_IBG_OctUnits.HasValue ? s.APD_IBG_OctUnits.Value : 0.0;
                                        nov_total += s.APD_IBG_NovUnits.HasValue ? s.APD_IBG_NovUnits.Value : 0.0;
                                        dec_total += s.APD_IBG_DecUnits.HasValue ? s.APD_IBG_DecUnits.Value : 0.0;
                                        apd_ibg_totalnumberofunits += (s.APD_IBG_OctUnits.HasValue ? s.APD_IBG_OctUnits.Value : 0.0) + (s.APD_IBG_NovUnits.HasValue ? s.APD_IBG_NovUnits.Value : 0.0) + (s.APD_IBG_DecUnits.HasValue ? s.APD_IBG_DecUnits.Value : 0.0);
                                    }
                                if (s.APD_ibg_SPStatusQ3.HasValue && s.APD_ibg_PAStatusQ3.HasValue)
                                    if ((s.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                        s.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit) ||
                                        (s.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Cancel &&
                                        s.APD_ibg_PAStatusQ3.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ3.Approved))
                                    {
                                        jan_total += s.APD_IBG_JanUnits.HasValue ? s.APD_IBG_JanUnits.Value : 0.0;
                                        feb_total += s.APD_IBG_FebUnits.HasValue ? s.APD_IBG_FebUnits.Value : 0.0;
                                        mar_total += s.APD_IBG_MarUnits.HasValue ? s.APD_IBG_MarUnits.Value : 0.0;
                                        apd_ibg_totalnumberofunits += (s.APD_IBG_JanUnits.HasValue ? s.APD_IBG_JanUnits.Value : 0.0) + (s.APD_IBG_FebUnits.HasValue ? s.APD_IBG_FebUnits.Value : 0.0) + (s.APD_IBG_MarUnits.HasValue ? s.APD_IBG_MarUnits.Value : 0.0);
                                    }
                                if (s.APD_ibg_SPStatusQ4.HasValue && s.APD_ibg_PAStatusQ4.HasValue)
                                    if ((s.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                        s.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit) ||
                                        (s.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Cancel &&
                                        s.APD_ibg_PAStatusQ4.Value != (int)APD_IBG_serviceplanAPD_ibg_PAStatusQ4.Approved))
                                    {
                                        apr_total += s.APD_IBG_AprUnits.HasValue ? s.APD_IBG_AprUnits.Value : 0.0;
                                        may_total += s.APD_IBG_MayUnits.HasValue ? s.APD_IBG_MayUnits.Value : 0.0;
                                        jun_total += s.APD_IBG_JunUnits.HasValue ? s.APD_IBG_JunUnits.Value : 0.0;
                                        apd_ibg_totalnumberofunits += (s.APD_IBG_AprUnits.HasValue ? s.APD_IBG_AprUnits.Value : 0.0) + (s.APD_IBG_MayUnits.HasValue ? s.APD_IBG_MayUnits.Value : 0.0) + (s.APD_IBG_JunUnits.HasValue ? s.APD_IBG_JunUnits.Value : 0.0);
                                    }
                            }

                            if (_maxMonthUnits != 9999999999)
                            {
                                #region Validate the totals for each month




                                if (jul_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("July Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + jul_total.ToString("N0") + " units.");
                                }

                                if (aug_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("August Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + aug_total.ToString("N0") + " units.");
                                }

                                if (sep_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("September Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + sep_total.ToString("N0") + " units.");
                                }

                                if (oct_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("October Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + oct_total.ToString("N0") + " units.");
                                }

                                if (nov_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("November Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + nov_total.ToString("N0") + " units.");
                                }

                                if (dec_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("December Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + dec_total.ToString("N0") + " units.");
                                }

                                if (jan_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("January Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + jan_total.ToString("N0") + " units.");
                                }

                                if (feb_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("February Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + feb_total.ToString("N0") + " units.");
                                }

                                if (mar_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("March Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + mar_total.ToString("N0") + " units.");
                                }

                                if (apr_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("April Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + apr_total.ToString("N0") + " units.");
                                }

                                if (may_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("May Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + may_total.ToString("N0") + " units.");
                                }

                                if (jun_total > _maxMonthUnits)
                                {
                                    throw new InvalidPluginExecutionException("June Units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxMonthUnits.ToString("N0") + " units per month. You have budgeted for " + jun_total.ToString("N0") + " units.");
                                }

                                #endregion
                            }

                            if (_maxYearlyUnits != 9999999999)
                            {
                                #region Validate the totals for entire year

                                if (apd_ibg_totalnumberofunits > _maxYearlyUnits)
                                    throw new InvalidPluginExecutionException("The total number of units for " + _servicecode_description + " exceeds the limit. Max Allowed is " + _maxYearlyUnits.ToString("N0") + " units per year. You have budgeted for " + apd_ibg_totalnumberofunits.ToString("N0") + " units.");
                                #endregion
                            }
                        }
                    }
                }
            }
        }

        private bool Validate_CriticalServices_CurrentApprovedCostPlan(XrmServiceContext iBudget, Entity original_entity, out string _scdescription, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc)
        {
            bool isValid = true;
            _scdescription = "";

            Guid _pAnnualCostPlanId = original_entity.Id;
            Guid _ConsumerId = ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id;
            Guid _fiscalYearId = ((EntityReference)original_entity.Attributes["apd_fiscalyearid"]).Id;

            #region Get Service Code Id, Service Description for Service Code with Critical Flag set to True

            var criticalServices = (from c in allsc
                                    where c.apd_PartofGroup == false
                                          && c.APD_IsCritical == true
                                    select new { c.APD_servicecodeId, c.APD_ServiceCodeName, c.APD_Description }).ToList();
            #endregion

            #region Get the Pre-Approved Services for the Consumer containing the above Service Code Id's

            if (criticalServices != null)
                if (criticalServices.Count != 0)
                {
                    List<APD_IBG_consumerapprovedservice> services = new List<APD_IBG_consumerapprovedservice>();

                    foreach (var svc in criticalServices)
                    {
                        APD_IBG_consumerapprovedservice temp = (from c in iBudget.APD_IBG_consumerapprovedserviceSet
                                                                where c.apd_consumerid.Id == _ConsumerId
                                                                      && c.apd_servicecodeid.Id == svc.APD_servicecodeId
                                                                select c).ToList().FirstOrDefault();
                        if (temp != null)
                            services.Add(temp);
                    }

                    if (services.Count > 0)
                    {
                        bool isValidTemp = true;

                        #region Get Service Plans for Annual Cost Plan Id & Service Code Id matching Critical Services
                        List<APD_IBG_serviceplan> serviceplans = new List<APD_IBG_serviceplan>();
                        foreach (var svc in services)
                        {
                            List<APD_IBG_serviceplan> temp = (from c in allsps
                                                              where c.apd_annualcostplanid.Id == _pAnnualCostPlanId
                                                                    && c.apd_servicecodeid.Id == svc.apd_servicecodeid.Id
                                                              select c).ToList();
                            serviceplans.AddRange(temp);

                            isValidTemp = true;
                            double jul_total_current = 0, aug_total_current = 0, sep_total_current = 0, oct_total_current = 0, nov_total_current = 0, dec_total_current = 0,
                            jan_total_current = 0, feb_total_current = 0, mar_total_current = 0, apr_total_current = 0, may_total_current = 0, jun_total_current = 0, apd_ibg_total_numberofunits_current = 0;

                            double jul_total_approved = 0, aug_total_approved = 0, sep_total_approved = 0, oct_total_approved = 0, nov_total_approved = 0, dec_total_approved = 0,
                            jan_total_approved = 0, feb_total_approved = 0, mar_total_approved = 0, apr_total_approved = 0, may_total_approved = 0, jun_total_approved = 0, apd_ibg_total_numberofunits_approved = 0;

                            Guid serviceCodeId = svc.apd_servicecodeid.Id;

                            var _cacp = (from a in iBudget.APD_IBG_annualcostplanSet
                                         where a.APD_ibg_ProcessingStatusCode.Value == (int)APD_IBG_annualcostplanAPD_ibg_ProcessingStatusCode.Approved
                                         && a.statuscode == (int)apd_ibg_annualcostplan_statuscode.CurrentApproved
                                         && a.apd_consumerid.Id == _ConsumerId
                                         && a.apd_fiscalyearid.Id == _fiscalYearId
                                         select new { a.APD_IBG_annualcostplanId }).ToList().FirstOrDefault();

                            if (_cacp != null)
                            {
                                var _cannualcostplanid = _cacp.APD_IBG_annualcostplanId.Value;

                                List<APD_IBG_serviceplan> approved = new List<APD_IBG_serviceplan>();
                                approved = (from a in iBudget.APD_IBG_serviceplanSet
                                            where a.apd_servicecodeid.Id == serviceCodeId &&
                                                  a.apd_annualcostplanid.Id == _cannualcostplanid
                                            select a).ToList();


                                foreach (var sp in serviceplans)
                                {
                                    if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                        if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                            sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                        {
                                            jul_total_current += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                            aug_total_current += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                            sep_total_current += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                        }

                                    if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                        if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                            sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                        {
                                            oct_total_current += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                            nov_total_current += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                            dec_total_current += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;
                                        }

                                    if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                        if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                            sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                        {
                                            jan_total_current += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                            feb_total_current += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                            mar_total_current += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                        }

                                    if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                        if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                            sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                        {
                                            apr_total_current += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                            may_total_current += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                            jun_total_current += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                        }
                                }


                                foreach (var sp in approved)
                                {
                                    if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                        if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                            sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                        {
                                            jul_total_approved += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                            aug_total_approved += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                            sep_total_approved += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                        }

                                    if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                        if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                            sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                        {
                                            oct_total_approved += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                            nov_total_approved += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                            dec_total_approved += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;
                                        }

                                    if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                        if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                            sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                        {
                                            jan_total_approved += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                            feb_total_approved += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                            mar_total_approved += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                        }

                                    if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                        if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                            sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                        {
                                            apr_total_approved += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                            may_total_approved += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                            jun_total_approved += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                        }
                                }

                                apd_ibg_total_numberofunits_current = jul_total_current + aug_total_current + sep_total_current + oct_total_current + nov_total_current + dec_total_current + jan_total_current + feb_total_current + mar_total_current + apr_total_current + may_total_current + jun_total_current;

                                apd_ibg_total_numberofunits_approved = jul_total_approved + aug_total_approved + sep_total_approved + oct_total_approved + nov_total_approved + dec_total_approved + jan_total_approved + feb_total_approved + mar_total_approved + apr_total_approved + may_total_approved + jun_total_approved;

                                if (apd_ibg_total_numberofunits_current - apd_ibg_total_numberofunits_approved < 0.0)
                                    isValidTemp = false;

                                if (!isValidTemp)
                                {
                                    _scdescription += criticalServices.Find(x => x.APD_servicecodeId.Value == serviceCodeId).APD_Description + ", ";
                                    isValid = isValidTemp;
                                    isValidTemp = true;
                                }
                            }
                        }
                        #endregion
                    }

                }
            #endregion

            return isValid;
        }

        private bool Validate_CriticalServices_PreviousFYCurrentApprovedCostPlan(XrmServiceContext iBudget, Entity original_entity, out string _scdescription, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc, List<APD_IBG_serviceplan> approved)
        {
            bool isValid = true;
            _scdescription = "";

            Guid _pAnnualCostPlanId = original_entity.Id;
            Guid _ConsumerId = ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id;
            Guid _fiscalYearId = ((EntityReference)original_entity.Attributes["apd_fiscalyearid"]).Id;

            #region Get Service Code Id, Service Description for Service Code with Critical Flag set to True

            var criticalServices = (from c in allsc
                                    where c.apd_PartofGroup == false
                                          && c.APD_IsCritical == true
                                    select new { c.APD_servicecodeId, c.APD_ServiceCodeName, c.APD_Description }).ToList();
            #endregion

            #region Get the Pre-Approved Services for the Consumer containing the above Service Code Id's

            if (criticalServices != null)
                if (criticalServices.Count != 0)
                {
                    List<APD_IBG_consumerapprovedservice> services = new List<APD_IBG_consumerapprovedservice>();

                    foreach (var svc in criticalServices)
                    {
                        APD_IBG_consumerapprovedservice temp = (from c in iBudget.APD_IBG_consumerapprovedserviceSet
                                                                where c.apd_consumerid.Id == _ConsumerId
                                                                      && c.apd_servicecodeid.Id == svc.APD_servicecodeId
                                                                select c).ToList().FirstOrDefault();
                        if (temp != null)
                            services.Add(temp);
                    }

                    if (services.Count > 0)
                    {
                        bool isValidTemp = true;

                        #region Get Service Plans for Annual Cost Plan Id & Service Code Id matching Critical Services
                        List<APD_IBG_serviceplan> serviceplans = new List<APD_IBG_serviceplan>();
                        List<APD_IBG_serviceplan> serviceplans_approved = new List<APD_IBG_serviceplan>();

                        foreach (var svc in services)
                        {
                            List<APD_IBG_serviceplan> temp = (from c in allsps
                                                              where c.apd_annualcostplanid.Id == _pAnnualCostPlanId
                                                                    && c.apd_servicecodeid.Id == svc.apd_servicecodeid.Id
                                                              select c).ToList();
                            serviceplans.AddRange(temp);

                            isValidTemp = true;
                            double jul_total_current = 0, aug_total_current = 0, sep_total_current = 0, oct_total_current = 0, nov_total_current = 0, dec_total_current = 0,
                            jan_total_current = 0, feb_total_current = 0, mar_total_current = 0, apr_total_current = 0, may_total_current = 0, jun_total_current = 0, apd_ibg_total_numberofunits_current = 0;

                            double jul_total_approved = 0, aug_total_approved = 0, sep_total_approved = 0, oct_total_approved = 0, nov_total_approved = 0, dec_total_approved = 0,
                            jan_total_approved = 0, feb_total_approved = 0, mar_total_approved = 0, apr_total_approved = 0, may_total_approved = 0, jun_total_approved = 0, apd_ibg_total_numberofunits_approved = 0;

                            Guid serviceCodeId = svc.apd_servicecodeid.Id;

                            foreach (var sp in serviceplans)
                            {
                                if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                    if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                        sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                    {
                                        jul_total_current += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                        aug_total_current += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                        sep_total_current += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                    if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                        sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                    {
                                        oct_total_current += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                        nov_total_current += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                        dec_total_current += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                    if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                        sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                    {
                                        jan_total_current += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                        feb_total_current += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                        mar_total_current += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                    if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                        sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                    {
                                        apr_total_current += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                        may_total_current += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                        jun_total_current += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                    }
                            }


                            List<APD_IBG_serviceplan> temp1 = (from c in approved
                                                               where c.apd_servicecodeid.Id == svc.apd_servicecodeid.Id
                                                               select c).ToList();
                            serviceplans_approved.AddRange(temp1);

                            foreach (var sp in serviceplans_approved)
                            {
                                if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                    if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                        sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                    {
                                        jul_total_approved += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                        aug_total_approved += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                        sep_total_approved += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                    if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                        sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                    {
                                        oct_total_approved += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                        nov_total_approved += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                        dec_total_approved += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                    if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                        sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                    {
                                        jan_total_approved += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                        feb_total_approved += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                        mar_total_approved += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                    if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                        sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                    {
                                        apr_total_approved += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                        may_total_approved += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                        jun_total_approved += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                    }
                            }

                            apd_ibg_total_numberofunits_current = jul_total_current + aug_total_current + sep_total_current + oct_total_current + nov_total_current + dec_total_current + jan_total_current + feb_total_current + mar_total_current + apr_total_current + may_total_current + jun_total_current;

                            apd_ibg_total_numberofunits_approved = jul_total_approved + aug_total_approved + sep_total_approved + oct_total_approved + nov_total_approved + dec_total_approved + jan_total_approved + feb_total_approved + mar_total_approved + apr_total_approved + may_total_approved + jun_total_approved;

                            if (apd_ibg_total_numberofunits_current - apd_ibg_total_numberofunits_approved < 0.0)
                                isValidTemp = false;

                            if (!isValidTemp)
                            {
                                _scdescription += criticalServices.Find(x => x.APD_servicecodeId.Value == serviceCodeId).APD_Description + ", ";
                                isValid = isValidTemp;
                                isValidTemp = true;
                            }

                        }
                        #endregion
                    }

                }
            #endregion

            return isValid;
        }

        protected bool Validate_CriticalServiceGroups(XrmServiceContext iBudget, Entity original_entity, string FY, out string _criticalgroups, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc, bool _iscopiedcostplan)
        {
            bool isValid_temp = true, _isValid = true;
            string _checkedGroups = _criticalgroups = "";

            Guid _pAnnualCostPlanId = original_entity.Id;
            Guid _ConsumerId = ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id;

            #region Get Service Code Id for all consumer approved services
            var approvedServices = (from c in iBudget.APD_IBG_consumerapprovedserviceSet
                                    where c.apd_consumerid.Id == _ConsumerId
                                          && c.APD_PriorAuthorizationNumber == "N/A"
                                          && c.statecode == 0
                                    select new { c.apd_servicecodeid }).ToList();
            #endregion

            #region get all services which are part of group

            if (approvedServices.Count != 0)
            {
                List<APD_servicecode> services = new List<APD_servicecode>();

                foreach (var item in approvedServices)
                {
                    APD_servicecode temp = (from c in allsc
                                            where c.APD_servicecodeId.Value == item.apd_servicecodeid.Id &&
                                                  c.APD_IsCritical == true &&
                                                  c.apd_PartofGroup == true
                                            select c).ToList().FirstOrDefault();

                    if (temp != null)
                        services.Add(temp);
                }

                var criticalgroupservices = (from cgs in iBudget.APD_criticalgroupservicesSet
                                             join cg in iBudget.APD_criticalgroupSet
                                             on cgs.APD_CriticalGroupId.Id equals cg.APD_criticalgroupId.Value
                                             where cgs.statecode == 0
                                             where cg.apd_AmountCheck.Value == (int)apd_yesorno.No
                                             select new { cgs, cg }).ToList();

                foreach (APD_servicecode sc in services)
                {
                    Guid _criticalgroupid = Guid.Empty; bool _sendforrereview = false;
                    var _criticalgroup = criticalgroupservices.Find(x => x.cgs.APD_ServiceCOdeId.Id == sc.APD_servicecodeId.Value);

                    if (_criticalgroup != null)
                    {
                        _criticalgroupid = _criticalgroup.cgs.APD_CriticalGroupId.Id;
                        _sendforrereview = Convert.ToBoolean(_criticalgroup.cg.apd_SendforRereview.Value);
                    }

                    if (!_checkedGroups.Contains(_criticalgroupid.ToString()) && _criticalgroupid != Guid.Empty)
                    {
                        var cgss = criticalgroupservices.FindAll(x => x.cgs.APD_CriticalGroupId.Id == _criticalgroupid).ToList();

                        List<APD_IBG_serviceplan> matchedsps = new List<APD_IBG_serviceplan>();

                        foreach (var s in cgss)
                        {
                            matchedsps.AddRange(allsps.FindAll(x => x.apd_servicecodeid.Id == s.cgs.APD_ServiceCOdeId.Id));
                        }

                        double jul_total = 0, aug_total = 0, sep_total = 0, oct_total = 0, nov_total = 0, dec_total = 0,
                            jan_total = 0, feb_total = 0, mar_total = 0, apr_total = 0, may_total = 0, jun_total = 0;

                        if (matchedsps != null)
                        {
                            foreach (var sp in matchedsps)
                            {
                                if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                    if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                        sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                    {
                                        jul_total += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                        aug_total += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                        sep_total += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                    if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                        sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                    {
                                        oct_total += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                        nov_total += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                        dec_total += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                    if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                        sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                    {
                                        jan_total += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                        feb_total += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                        mar_total += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                    if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                        sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                    {
                                        apr_total += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                        may_total += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                        jun_total += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                    }
                            }

                        }

                        if (ReturnMonthStart(7, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((jul_total == 0 && !_iscopiedcostplan) || (jul_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(8, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((aug_total == 0 && !_iscopiedcostplan) || (aug_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(9, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((sep_total == 0 && !_iscopiedcostplan) || (sep_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(10, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((oct_total == 0 && !_iscopiedcostplan) || (oct_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(11, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((nov_total == 0 && !_iscopiedcostplan) || (nov_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(12, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((dec_total == 0 && !_iscopiedcostplan) || (dec_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(1, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((jan_total == 0 && !_iscopiedcostplan) || (jan_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(2, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((feb_total == 0 && !_iscopiedcostplan) || (feb_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(3, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((mar_total == 0 && !_iscopiedcostplan) || (mar_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(4, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((apr_total == 0 && !_iscopiedcostplan) || (apr_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(5, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((may_total == 0 && !_iscopiedcostplan) || (may_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        if (ReturnMonthStart(6, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if ((jun_total == 0 && !_iscopiedcostplan) || (jun_total == 0 && _iscopiedcostplan && _sendforrereview))
                            {
                                isValid_temp = false;
                            }
                        }
                        _checkedGroups += _criticalgroupid.ToString() + ":";

                        if (!isValid_temp)
                        {
                            _criticalgroups += (from s in iBudget.APD_criticalgroupSet where s.APD_criticalgroupId.Value == _criticalgroupid select new { s.APD_name }).ToList().First().APD_name + ", ";
                            isValid_temp = true;
                            _isValid = false;
                        }
                    }
                }
            }

            #endregion

            return _isValid;
        }

        protected bool Validate_CriticalServiceGroups_CurrentApprovedCostPlan(XrmServiceContext iBudget, Entity original_entity, string FY, out string _criticalgroups, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc, List<APD_IBG_serviceplan> approved)
        {
            bool isValid_temp = true, _isValid = true;
            string _checkedGroups = _criticalgroups = "";

            Guid _pAnnualCostPlanId = original_entity.Id;
            Guid _ConsumerId = ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id;

            #region Get Service Code Id for all consumer approved services
            var approvedServices = (from c in iBudget.APD_IBG_consumerapprovedserviceSet
                                    where c.apd_consumerid.Id == _ConsumerId
                                          && c.APD_PriorAuthorizationNumber == "N/A"
                                          && c.statecode == 0
                                    select new { c.apd_servicecodeid }).ToList();
            #endregion

            #region get all services which are part of group

            if (approvedServices.Count != 0)
            {
                List<APD_servicecode> services = new List<APD_servicecode>();

                foreach (var item in approvedServices)
                {
                    APD_servicecode temp = (from c in allsc
                                            where c.APD_servicecodeId.Value == item.apd_servicecodeid.Id &&
                                                  c.APD_IsCritical == true &&
                                                  c.apd_PartofGroup == true
                                            select c).ToList().FirstOrDefault();

                    if (temp != null)
                        services.Add(temp);
                }

                var criticalgroupservices = (from cgs in iBudget.APD_criticalgroupservicesSet
                                             join cg in iBudget.APD_criticalgroupSet
                                             on cgs.APD_CriticalGroupId.Id equals cg.APD_criticalgroupId.Value
                                             where cgs.statecode == 0
                                             where cg.apd_AmountCheck.Value == (int)apd_yesorno.No
                                             select new { cgs, cg }).ToList();

                foreach (APD_servicecode sc in services)
                {
                    Guid _criticalgroupid = Guid.Empty; bool _sendforrereview = false;
                    var _criticalgroup = criticalgroupservices.Find(x => x.cgs.APD_ServiceCOdeId.Id == sc.APD_servicecodeId.Value);

                    if (_criticalgroup != null)
                    {
                        _criticalgroupid = _criticalgroup.cgs.APD_CriticalGroupId.Id;
                        _sendforrereview = Convert.ToBoolean(_criticalgroup.cg.apd_SendforRereview.Value);
                    }

                    if (!_checkedGroups.Contains(_criticalgroupid.ToString()) && _criticalgroupid != Guid.Empty)
                    {
                        var cgss = criticalgroupservices.FindAll(x => x.cgs.APD_CriticalGroupId.Id == _criticalgroupid).ToList();

                        List<APD_IBG_serviceplan> matchedsps = new List<APD_IBG_serviceplan>();
                        List<APD_IBG_serviceplan> approvedsps = new List<APD_IBG_serviceplan>();

                        foreach (var s in cgss)
                        {
                            matchedsps.AddRange(allsps.FindAll(x => x.apd_servicecodeid.Id == s.cgs.APD_ServiceCOdeId.Id));
                            approvedsps.AddRange(approved.FindAll(x => x.apd_servicecodeid.Id == s.cgs.APD_ServiceCOdeId.Id));
                        }

                        double current_total = 0, approved_total = 0;

                        if (matchedsps != null)
                        {
                            foreach (var sp in matchedsps)
                            {
                                if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                    if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                        sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                    {
                                        current_total += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                        current_total += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                        current_total += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                    if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                        sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                    {
                                        current_total += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                        current_total += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                        current_total += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                    if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                        sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                    {
                                        current_total += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                        current_total += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                        current_total += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                    if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                        sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                    {
                                        current_total += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                        current_total += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                        current_total += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                    }
                            }

                        }

                        if (approvedsps != null)
                        {
                            foreach (var sp in approvedsps)
                            {
                                if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                    if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                        sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                    {
                                        approved_total += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                        approved_total += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                        approved_total += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                    if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                        sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                    {
                                        approved_total += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                        approved_total += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                        approved_total += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                    if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                        sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                    {
                                        approved_total += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                        approved_total += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                        approved_total += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                    if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                        sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                    {
                                        approved_total += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                        approved_total += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                        approved_total += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                    }
                            }

                        }


                        if (current_total < approved_total)
                        {
                            isValid_temp = false;
                        }

                        _checkedGroups += _criticalgroupid.ToString() + ":";

                        if (!isValid_temp)
                        {
                            _criticalgroups += (from s in iBudget.APD_criticalgroupSet where s.APD_criticalgroupId.Value == _criticalgroupid select new { s.APD_name }).ToList().First().APD_name + ", ";
                            isValid_temp = true;
                            _isValid = false;
                        }
                    }
                }
            }

            #endregion

            return _isValid;
        }

        protected bool Validate_CriticalServiceGroupAOAmounts(XrmServiceContext iBudget, Entity original_entity, out string _criticalgroups, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc, List<APD_IBG_serviceplan> currentFYsps)
        {
            bool isValid_temp = true, _isValid = true;
            string _checkedGroups = _criticalgroups = "Current Total of ";

            Guid _pAnnualCostPlanId = original_entity.Id;
            Guid _ConsumerId = ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id;

            #region Get Service Code Id for all consumer approved services
            var approvedServices = (from c in iBudget.APD_IBG_consumerapprovedserviceSet
                                    where c.apd_consumerid.Id == _ConsumerId
                                          && c.APD_PriorAuthorizationNumber == "N/A"
                                          && c.statecode == 0
                                    select new { c.apd_servicecodeid }).ToList();
            #endregion

            #region get all services which are part of group

            if (approvedServices.Count != 0)
            {
                List<APD_servicecode> services = new List<APD_servicecode>();

                foreach (var item in approvedServices)
                {
                    APD_servicecode temp = (from c in allsc
                                            where c.APD_servicecodeId.Value == item.apd_servicecodeid.Id &&
                                                  c.APD_IsCritical == true &&
                                                  c.apd_PartofGroup == true
                                            select c).ToList().FirstOrDefault();

                    if (temp != null)
                        services.Add(temp);
                }

                var criticalgroupservices = (from cgs in iBudget.APD_criticalgroupservicesSet
                                             join cg in iBudget.APD_criticalgroupSet
                                             on cgs.APD_CriticalGroupId.Id equals cg.APD_criticalgroupId.Value
                                             where cgs.statecode == 0
                                             where cg.apd_AmountCheck.Value == (int)apd_yesorno.Yes
                                             select cgs).ToList();

                foreach (APD_servicecode sc in services)
                {
                    Guid _criticalgroupid = Guid.Empty;
                    var _criticalgroup = criticalgroupservices.Find(x => x.APD_ServiceCOdeId.Id == sc.APD_servicecodeId.Value);

                    if (_criticalgroup != null)
                        _criticalgroupid = _criticalgroup.APD_CriticalGroupId.Id;

                    if (!_checkedGroups.Contains(_criticalgroupid.ToString()) && _criticalgroupid != Guid.Empty)
                    {
                        var cgss = criticalgroupservices.FindAll(x => x.APD_CriticalGroupId.Id == _criticalgroupid).ToList();

                        List<APD_IBG_serviceplan> matchedsps = new List<APD_IBG_serviceplan>();
                        List<APD_IBG_serviceplan> previousmatchedsps = new List<APD_IBG_serviceplan>();

                        foreach (var s in cgss)
                        {
                            matchedsps.AddRange(allsps.FindAll(x => x.apd_servicecodeid.Id == s.APD_ServiceCOdeId.Id));

                            previousmatchedsps.AddRange(currentFYsps.FindAll(x => x.apd_servicecodeid.Id == s.APD_ServiceCOdeId.Id));
                        }

                        Decimal total = 0, previoustotal = 0;


                        var cgs = (from s in iBudget.APD_criticalgroupSet where s.APD_criticalgroupId.Value == _criticalgroupid select s).ToList().First();
                        string range = cgs.apd_AreaOfficeApprovalAmounts;

                        if (!range.ToLower().Contains("none"))
                        {
                            Decimal lowerLimit = Convert.ToDecimal(range.Substring(0, range.IndexOf("-")).Replace("$", "").Replace(",", ""));
                            Decimal upperLimit = Convert.ToDecimal(range.Substring(range.IndexOf("-") + 1, range.Length - 1 - range.IndexOf("-")).Replace("$", "").Replace(",", ""));

                            foreach (var sp in matchedsps)
                            {
                                if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New || sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                    total += sp.APD_ibg_SPAmountQ1.Value;
                                if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New || sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                    total += sp.APD_ibg_SPAmountQ2.Value;
                                if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New || sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                    total += sp.APD_ibg_SPAmountQ3.Value;
                                if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New || sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                    total += sp.APD_ibg_SPAmountQ4.Value;
                            }
                            foreach (var sp in previousmatchedsps)
                            {
                                if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New || sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                    previoustotal += sp.APD_ibg_SPAmountQ1.Value;
                                if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New || sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                    previoustotal += sp.APD_ibg_SPAmountQ2.Value;
                                if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New || sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                    previoustotal += sp.APD_ibg_SPAmountQ3.Value;
                                if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New || sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                    previoustotal += sp.APD_ibg_SPAmountQ4.Value;
                            }

                            if (total >= lowerLimit && total <= upperLimit)
                            {
                                isValid_temp = false;
                            }
                            _checkedGroups += _criticalgroupid.ToString() + ":";

                            if (!isValid_temp && total > previoustotal)
                            {
                                _criticalgroups += "$" + total.ToString("N0") + " for " + cgs.APD_name + " exceeds the current year approved total of $" + previoustotal.ToString("N0") + " and is in the threshold limits of " + cgs.apd_AreaOfficeApprovalAmounts + " for Area Review; ";
                                isValid_temp = true;
                                _isValid = false;
                            }
                        }
                    }
                }
            }

            #endregion

            return _isValid;
        }

        protected bool Validate_PreviousFYCriticalServiceGroupAOAmounts(XrmServiceContext iBudget, Entity original_entity, out string _criticalgroups, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc, List<APD_IBG_serviceplan> previousFYsps)
        {
            bool isValid_temp = true, _isValid = true;
            string _checkedGroups = _criticalgroups = "Current Total of ";

            Guid _pAnnualCostPlanId = original_entity.Id;
            Guid _ConsumerId = ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id;

            #region Get Service Code Id for all consumer approved services
            var approvedServices = (from c in iBudget.APD_IBG_consumerapprovedserviceSet
                                    where c.apd_consumerid.Id == _ConsumerId
                                          && c.APD_PriorAuthorizationNumber == "N/A"
                                          && c.statecode == 0
                                    select new { c.apd_servicecodeid }).ToList();
            #endregion

            #region get all services which are part of group

            if (approvedServices.Count != 0)
            {
                List<APD_servicecode> services = new List<APD_servicecode>();

                foreach (var item in approvedServices)
                {
                    APD_servicecode temp = (from c in allsc
                                            where c.APD_servicecodeId.Value == item.apd_servicecodeid.Id &&
                                                  c.APD_IsCritical == true &&
                                                  c.apd_PartofGroup == true
                                            select c).ToList().FirstOrDefault();

                    if (temp != null)
                        services.Add(temp);
                }

                var criticalgroupservices = (from cgs in iBudget.APD_criticalgroupservicesSet
                                             join cg in iBudget.APD_criticalgroupSet
                                             on cgs.APD_CriticalGroupId.Id equals cg.APD_criticalgroupId.Value
                                             where cgs.statecode == 0
                                             where cg.apd_AmountCheck.Value == (int)apd_yesorno.Yes
                                             select cgs).ToList();

                foreach (APD_servicecode sc in services)
                {
                    Guid _criticalgroupid = Guid.Empty;
                    var _criticalgroup = criticalgroupservices.Find(x => x.APD_ServiceCOdeId.Id == sc.APD_servicecodeId.Value);

                    if (_criticalgroup != null)
                        _criticalgroupid = _criticalgroup.APD_CriticalGroupId.Id;

                    if (!_checkedGroups.Contains(_criticalgroupid.ToString()) && _criticalgroupid != Guid.Empty)
                    {
                        var cgss = criticalgroupservices.FindAll(x => x.APD_CriticalGroupId.Id == _criticalgroupid).ToList();

                        List<APD_IBG_serviceplan> matchedsps = new List<APD_IBG_serviceplan>();
                        List<APD_IBG_serviceplan> previousmatchedsps = new List<APD_IBG_serviceplan>();

                        foreach (var s in cgss)
                        {
                            matchedsps.AddRange(allsps.FindAll(x => x.apd_servicecodeid.Id == s.APD_ServiceCOdeId.Id));

                            previousmatchedsps.AddRange(previousFYsps.FindAll(x => x.apd_servicecodeid.Id == s.APD_ServiceCOdeId.Id));
                        }

                        Decimal total = 0, previoustotal = 0;


                        var cgs = (from s in iBudget.APD_criticalgroupSet where s.APD_criticalgroupId.Value == _criticalgroupid select s).ToList().First();
                        string range = cgs.apd_AreaOfficeApprovalAmounts;

                        if (!range.ToLower().Contains("none"))
                        {
                            Decimal lowerLimit = Convert.ToDecimal(range.Substring(0, range.IndexOf("-")).Replace("$", "").Replace(",", ""));
                            Decimal upperLimit = Convert.ToDecimal(range.Substring(range.IndexOf("-") + 1, range.Length - 1 - range.IndexOf("-")).Replace("$", "").Replace(",", ""));

                            foreach (var sp in matchedsps)
                            {
                                total += sp.APD_IBG_TotalAmount.Value;
                            }
                            foreach (var sp in previousmatchedsps)
                            {
                                previoustotal += sp.APD_IBG_TotalAmount.Value;
                            }

                            if (total >= lowerLimit && total <= upperLimit)
                            {
                                isValid_temp = false;
                            }
                            _checkedGroups += _criticalgroupid.ToString() + ":";

                            if (!isValid_temp && total > previoustotal)
                            {
                                _criticalgroups += "$" + total.ToString("N0") + " for " + cgs.APD_name + " exceeds the previous year total of $" + previoustotal.ToString("N0") + " and is in the threshold limits of " + cgs.apd_AreaOfficeApprovalAmounts + " for Area Review; ";
                                isValid_temp = true;
                                _isValid = false;
                            }
                        }
                    }
                }
            }

            #endregion

            return _isValid;
        }

        protected bool Validate_CriticalServiceGroupCOAmounts(XrmServiceContext iBudget, Entity original_entity, out string _criticalgroups, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc, List<APD_IBG_serviceplan> CurrentFYsps)
        {
            bool isValid_temp = true, _isValid = true;
            string _checkedGroups = _criticalgroups = "Current Total of ";

            Guid _pAnnualCostPlanId = original_entity.Id;
            Guid _ConsumerId = ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id;

            #region Get Service Code Id for all consumer approved services
            var approvedServices = (from c in iBudget.APD_IBG_consumerapprovedserviceSet
                                    where c.apd_consumerid.Id == _ConsumerId
                                          && c.APD_PriorAuthorizationNumber == "N/A"
                                          && c.statecode == 0
                                    select new { c.apd_servicecodeid }).ToList();
            #endregion

            #region get all services which are part of group

            if (approvedServices.Count != 0)
            {
                List<APD_servicecode> services = new List<APD_servicecode>();

                foreach (var item in approvedServices)
                {
                    APD_servicecode temp = (from c in allsc
                                            where c.APD_servicecodeId.Value == item.apd_servicecodeid.Id &&
                                                  c.APD_IsCritical == true &&
                                                  c.apd_PartofGroup == true
                                            select c).ToList().FirstOrDefault();

                    if (temp != null)
                        services.Add(temp);
                }

                var criticalgroupservices = (from cgs in iBudget.APD_criticalgroupservicesSet
                                             join cg in iBudget.APD_criticalgroupSet
                                             on cgs.APD_CriticalGroupId.Id equals cg.APD_criticalgroupId.Value
                                             where cgs.statecode == 0
                                             where cg.apd_AmountCheck.Value == (int)apd_yesorno.Yes
                                             select cgs).ToList();



                foreach (APD_servicecode sc in services)
                {
                    Guid _criticalgroupid = Guid.Empty;
                    var _criticalgroup = criticalgroupservices.Find(x => x.APD_ServiceCOdeId.Id == sc.APD_servicecodeId.Value);

                    if (_criticalgroup != null)
                        _criticalgroupid = _criticalgroup.APD_CriticalGroupId.Id;

                    if (!_checkedGroups.Contains(_criticalgroupid.ToString()) && _criticalgroupid != Guid.Empty)
                    {
                        var cgss = criticalgroupservices.FindAll(x => x.APD_CriticalGroupId.Id == _criticalgroupid).ToList();

                        List<APD_IBG_serviceplan> matchedsps = new List<APD_IBG_serviceplan>();
                        List<APD_IBG_serviceplan> previousmatchedsps = new List<APD_IBG_serviceplan>();

                        foreach (var s in cgss)
                        {
                            matchedsps.AddRange(allsps.FindAll(x => x.apd_servicecodeid.Id == s.APD_ServiceCOdeId.Id));

                            previousmatchedsps.AddRange(CurrentFYsps.FindAll(x => x.apd_servicecodeid.Id == s.APD_ServiceCOdeId.Id));
                        }

                        Decimal total = 0, previoustotal = 0;


                        var cgs = (from s in iBudget.APD_criticalgroupSet where s.APD_criticalgroupId.Value == _criticalgroupid select s).ToList().First();
                        string range = cgs.apd_StateOfficeApprovalAmounts;

                        if (!range.ToLower().Contains("none"))
                        {
                            Decimal lowerLimit = Convert.ToDecimal(range.Substring(0, range.IndexOf("-")).Replace("$", "").Replace(",", ""));
                            Decimal upperLimit = Convert.ToDecimal(range.Substring(range.IndexOf("-") + 1, range.Length - 1 - range.IndexOf("-")).Replace("$", "").Replace(",", ""));

                            foreach (var sp in matchedsps)
                            {
                                if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New || sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                    total += sp.APD_ibg_SPAmountQ1.Value;
                                if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New || sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                    total += sp.APD_ibg_SPAmountQ2.Value;
                                if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New || sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                    total += sp.APD_ibg_SPAmountQ3.Value;
                                if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New || sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                    total += sp.APD_ibg_SPAmountQ4.Value;
                            }
                            foreach (var sp in previousmatchedsps)
                            {
                                if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New || sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                    previoustotal += sp.APD_ibg_SPAmountQ1.Value;
                                if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New || sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                    previoustotal += sp.APD_ibg_SPAmountQ2.Value;
                                if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New || sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                    previoustotal += sp.APD_ibg_SPAmountQ3.Value;
                                if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New || sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                    previoustotal += sp.APD_ibg_SPAmountQ4.Value;
                            }

                            if (total >= lowerLimit && total <= upperLimit)
                            {
                                isValid_temp = false;
                            }
                            _checkedGroups += _criticalgroupid.ToString() + ":";

                            if (!isValid_temp && total > previoustotal)
                            {
                                _criticalgroups += "$" + total.ToString("N0") + " for " + cgs.APD_name + " exceeds the current year approved total of $" + previoustotal.ToString("N0") + " and is in the threshold limits of " + cgs.apd_StateOfficeApprovalAmounts + " for State Office Review; ";
                                isValid_temp = true;
                                _isValid = false;
                            }
                        }
                    }
                }
            }

            #endregion

            return _isValid;
        }

        protected bool Validate_PreviousFYCriticalServiceGroupCOAmounts(XrmServiceContext iBudget, Entity original_entity, out string _criticalgroups, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc, List<APD_IBG_serviceplan> previousFYsps)
        {
            bool isValid_temp = true, _isValid = true;
            string _checkedGroups = _criticalgroups = "Current Total of ";

            Guid _pAnnualCostPlanId = original_entity.Id;
            Guid _ConsumerId = ((EntityReference)original_entity.Attributes["apd_consumerid"]).Id;

            #region Get Service Code Id for all consumer approved services
            var approvedServices = (from c in iBudget.APD_IBG_consumerapprovedserviceSet
                                    where c.apd_consumerid.Id == _ConsumerId
                                          && c.APD_PriorAuthorizationNumber == "N/A"
                                          && c.statecode == 0
                                    select new { c.apd_servicecodeid }).ToList();
            #endregion

            #region get all services which are part of group

            if (approvedServices.Count != 0)
            {
                List<APD_servicecode> services = new List<APD_servicecode>();

                foreach (var item in approvedServices)
                {
                    APD_servicecode temp = (from c in allsc
                                            where c.APD_servicecodeId.Value == item.apd_servicecodeid.Id &&
                                                  c.APD_IsCritical == true &&
                                                  c.apd_PartofGroup == true
                                            select c).ToList().FirstOrDefault();

                    if (temp != null)
                        services.Add(temp);
                }

                var criticalgroupservices = (from cgs in iBudget.APD_criticalgroupservicesSet
                                             join cg in iBudget.APD_criticalgroupSet
                                             on cgs.APD_CriticalGroupId.Id equals cg.APD_criticalgroupId.Value
                                             where cgs.statecode == 0
                                             where cg.apd_AmountCheck.Value == (int)apd_yesorno.Yes
                                             select cgs).ToList();

                foreach (APD_servicecode sc in services)
                {
                    Guid _criticalgroupid = Guid.Empty;
                    var _criticalgroup = criticalgroupservices.Find(x => x.APD_ServiceCOdeId.Id == sc.APD_servicecodeId.Value);

                    if (_criticalgroup != null)
                        _criticalgroupid = _criticalgroup.APD_CriticalGroupId.Id;

                    if (!_checkedGroups.Contains(_criticalgroupid.ToString()) && _criticalgroupid != Guid.Empty)
                    {
                        var cgss = criticalgroupservices.FindAll(x => x.APD_CriticalGroupId.Id == _criticalgroupid).ToList();

                        List<APD_IBG_serviceplan> matchedsps = new List<APD_IBG_serviceplan>();
                        List<APD_IBG_serviceplan> previousmatchedsps = new List<APD_IBG_serviceplan>();

                        foreach (var s in cgss)
                        {
                            matchedsps.AddRange(allsps.FindAll(x => x.apd_servicecodeid.Id == s.APD_ServiceCOdeId.Id));

                            previousmatchedsps.AddRange(previousFYsps.FindAll(x => x.apd_servicecodeid.Id == s.APD_ServiceCOdeId.Id));
                        }

                        Decimal total = 0, previoustotal = 0;


                        var cgs = (from s in iBudget.APD_criticalgroupSet where s.APD_criticalgroupId.Value == _criticalgroupid select s).ToList().First();
                        string range = cgs.apd_StateOfficeApprovalAmounts.ToString();

                        if (!range.ToLower().Contains("none"))
                        {
                            Decimal lowerLimit = Convert.ToDecimal(range.Substring(0, range.IndexOf("-")).Replace("$", "").Replace(",", ""));
                            Decimal upperLimit = Convert.ToDecimal(range.Substring(range.IndexOf("-") + 1, range.Length - 1 - range.IndexOf("-")).Replace("$", "").Replace(",", ""));

                            foreach (var sp in matchedsps)
                            {
                                total += sp.APD_IBG_TotalAmount.Value;
                            }
                            foreach (var sp in previousmatchedsps)
                            {
                                previoustotal += sp.APD_IBG_TotalAmount.Value;
                            }

                            if (total >= lowerLimit && total <= upperLimit)
                            {
                                isValid_temp = false;
                            }
                            _checkedGroups += _criticalgroupid.ToString() + ":";

                            if (!isValid_temp && total > previoustotal)
                            {
                                _criticalgroups += "$" + total.ToString("N0") + " for " + cgs.APD_name + " exceeds the previous year total of $" + previoustotal.ToString("N0") + " and is in the threshold limits of " + cgs.apd_StateOfficeApprovalAmounts + " for State Office Review; ";
                                isValid_temp = true;
                                _isValid = false;
                            }
                        }
                    }
                }
            }

            #endregion

            return _isValid;
        }

        private bool Validate_CriticalServices_Required(XrmServiceContext iBudget, Entity original_entity, out string _servicecode_nameordescription, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc)
        {
            bool isValid = true;
            _servicecode_nameordescription = "";

            Guid _pAnnualCostPlanId = original_entity.Id;

            #region Get Service Code Id, Service Description for Service Code assuming Service Code will be unique

            List<APD_servicecode> services = (from c in allsc
                                              where (c.APD_ServiceCodeName != "4270" &&
                                              c.APD_ServiceCodeName != "4271" &&
                                              c.APD_ServiceCodeName != "4272" &&
                                              c.APD_ServiceCodeName != "4400" &&
                                              c.APD_ServiceCodeName != "4410" &&
                                              c.APD_ServiceCodeName != "4411" &&
                                              c.APD_ServiceCodeName != "4412" &&
                                              c.APD_ServiceCodeName != "4414")
                                              && c.APD_IsCritical == true
                                              && c.apd_PartofGroup == false
                                              select c).ToList();


            if (services != null)
                if (services.Count > 0)
                {
                    List<APD_IBG_consumerapprovedservice> cas = (from c in iBudget.APD_IBG_consumerapprovedserviceSet
                                                                 where c.statecode == 0
                                                                 && c.apd_consumerid.Id == ((EntityReference)original_entity["apd_consumerid"]).Id
                                                                 select c).ToList();

                    #region Get Service Plans for Annual Cost Plan Id & Service Code Id

                    List<APD_IBG_serviceplan> serviceplans = new List<APD_IBG_serviceplan>();

                    bool isValidTemp = true;
                    foreach (var svc in cas)
                    {
                        if (services.FindAll(x => x.APD_servicecodeId.Value == svc.apd_servicecodeid.Id).Count > 0)
                        {
                            List<APD_IBG_serviceplan> temp = (from c in allsps
                                                              where c.apd_annualcostplanid.Id == _pAnnualCostPlanId &&
                                                                    c.apd_servicecodeid.Id == svc.apd_servicecodeid.Id
                                                              select c).ToList();
                            if (temp != null)
                                foreach (var t in temp)
                                    serviceplans.AddRange(temp);

                            #region Check Total Number of Units for each Service Plan & Max Units supplied

                            Double apd_ibg_totalunits = 0;

                            // Calculate the totals of the Service Plans found
                            foreach (var sp in serviceplans)
                            {
                                if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                    if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                        sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                    {
                                        apd_ibg_totalunits += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                        apd_ibg_totalunits += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                        apd_ibg_totalunits += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                    if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                        sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                    {
                                        apd_ibg_totalunits += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                        apd_ibg_totalunits += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                        apd_ibg_totalunits += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                    if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                        sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                    {
                                        apd_ibg_totalunits += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                        apd_ibg_totalunits += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                        apd_ibg_totalunits += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                    }

                                if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                    if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                        sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                    {
                                        apd_ibg_totalunits += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                        apd_ibg_totalunits += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                        apd_ibg_totalunits += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                    }
                            }

                            if (apd_ibg_totalunits == 0)
                                isValidTemp = false;

                            if (!isValidTemp)
                            {
                                _servicecode_nameordescription += (from p in services where p.APD_servicecodeId.Value == svc.apd_servicecodeid.Id select new { p.APD_Description }).FirstOrDefault().APD_Description + ", ";
                                isValidTemp = true;
                                isValid = false;
                            }

                            #endregion
                        }
                    }

                    #endregion
                }

            return isValid;

            #endregion
        }

        private bool Validate_SupportCoordination_Required(XrmServiceContext iBudget, Entity original_entity, string FY, out string _months, List<APD_IBG_serviceplan> allsps, List<APD_servicecode> allsc)
        {
            bool isValid = true; _months = "";

            return isValid;

            Guid _pAnnualCostPlanId = original_entity.Id;

            List<APD_IBG_serviceplan> matchedsps = new List<APD_IBG_serviceplan>();

            #region Get Service Code Id, Service Description for Service Code assuming Service Code will be unique

            List<APD_servicecode> services = (from c in allsc
                                              where (c.APD_ServiceCodeName == "4270" ||
                                              c.APD_ServiceCodeName == "4271" ||
                                              c.APD_ServiceCodeName == "4272" ||
                                              c.APD_ServiceCodeName == "4400" ||
                                              c.APD_ServiceCodeName == "4410" ||
                                              c.APD_ServiceCodeName == "4411" ||
                                              c.APD_ServiceCodeName == "4412" ||
                                              c.APD_ServiceCodeName == "4414")
                                              select c).ToList();


            if (services != null)
                if (services.Count > 0)
                {
                    foreach (APD_servicecode sc in services)
                    {
                        matchedsps.AddRange(allsps.FindAll(x => x.apd_servicecodeid.Id == sc.APD_servicecodeId.Value));
                    }

                    double jul_total = 0, aug_total = 0, sep_total = 0, oct_total = 0, nov_total = 0, dec_total = 0,
                        jan_total = 0, feb_total = 0, mar_total = 0, apr_total = 0, may_total = 0, jun_total = 0;

                    if (matchedsps != null)
                    {
                        foreach (var sp in matchedsps)
                        {
                            if (sp.APD_ibg_SPStatusQ1.HasValue && sp.APD_ibg_PAStatusQ1.HasValue)
                                if (sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.New ||
                                    sp.APD_ibg_SPStatusQ1.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ1.Edit)
                                {
                                    jul_total += sp.APD_IBG_JulUnits.HasValue ? sp.APD_IBG_JulUnits.Value : 0.0;
                                    aug_total += sp.APD_IBG_AugUnits.HasValue ? sp.APD_IBG_AugUnits.Value : 0.0;
                                    sep_total += sp.APD_IBG_SepUnits.HasValue ? sp.APD_IBG_SepUnits.Value : 0.0;
                                }

                            if (sp.APD_ibg_SPStatusQ2.HasValue && sp.APD_ibg_PAStatusQ2.HasValue)
                                if (sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.New ||
                                    sp.APD_ibg_SPStatusQ2.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ2.Edit)
                                {
                                    oct_total += sp.APD_IBG_OctUnits.HasValue ? sp.APD_IBG_OctUnits.Value : 0.0;
                                    nov_total += sp.APD_IBG_NovUnits.HasValue ? sp.APD_IBG_NovUnits.Value : 0.0;
                                    dec_total += sp.APD_IBG_DecUnits.HasValue ? sp.APD_IBG_DecUnits.Value : 0.0;
                                }

                            if (sp.APD_ibg_SPStatusQ3.HasValue && sp.APD_ibg_PAStatusQ3.HasValue)
                                if (sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.New ||
                                    sp.APD_ibg_SPStatusQ3.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ3.Edit)
                                {
                                    jan_total += sp.APD_IBG_JanUnits.HasValue ? sp.APD_IBG_JanUnits.Value : 0.0;
                                    feb_total += sp.APD_IBG_FebUnits.HasValue ? sp.APD_IBG_FebUnits.Value : 0.0;
                                    mar_total += sp.APD_IBG_MarUnits.HasValue ? sp.APD_IBG_MarUnits.Value : 0.0;
                                }

                            if (sp.APD_ibg_SPStatusQ4.HasValue && sp.APD_ibg_PAStatusQ4.HasValue)
                                if (sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.New ||
                                    sp.APD_ibg_SPStatusQ4.Value == (int)APD_IBG_serviceplanAPD_ibg_SPStatusQ4.Edit)
                                {
                                    apr_total += sp.APD_IBG_AprUnits.HasValue ? sp.APD_IBG_AprUnits.Value : 0.0;
                                    may_total += sp.APD_IBG_MayUnits.HasValue ? sp.APD_IBG_MayUnits.Value : 0.0;
                                    jun_total += sp.APD_IBG_JunUnits.HasValue ? sp.APD_IBG_JunUnits.Value : 0.0;
                                }
                        }

                        if (ReturnMonthStart(7, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (jul_total == 0)
                            {
                                isValid = false;
                                _months += " July, ";
                            }
                        }
                        if (ReturnMonthStart(8, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (aug_total == 0)
                            {
                                isValid = false;
                                _months += " August, ";
                            }
                        }
                        if (ReturnMonthStart(9, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (sep_total == 0)
                            {
                                isValid = false;
                                _months += " September, ";
                            }
                        }
                        if (ReturnMonthStart(10, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (oct_total == 0)
                            {
                                isValid = false;
                                _months += " October, ";
                            }
                        }
                        if (ReturnMonthStart(11, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (nov_total == 0)
                            {
                                isValid = false;
                                _months += " November, ";
                            }
                        }
                        if (ReturnMonthStart(12, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (dec_total == 0)
                            {
                                isValid = false;
                                _months += " December, ";
                            }
                        }
                        if (ReturnMonthStart(1, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (jan_total == 0)
                            {
                                isValid = false;
                                _months += " January, ";
                            }
                        }
                        if (ReturnMonthStart(2, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (feb_total == 0)
                            {
                                isValid = false;
                                _months += " February, ";
                            }
                        }
                        if (ReturnMonthStart(3, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (mar_total == 0)
                            {
                                isValid = false;
                                _months += " March, ";
                            }
                        }
                        if (ReturnMonthStart(4, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (apr_total == 0)
                            {
                                isValid = false;
                                _months += " April, ";
                            }
                        }
                        if (ReturnMonthStart(5, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (may_total == 0)
                            {
                                isValid = false;
                                _months += " May, ";
                            }
                        }
                        if (ReturnMonthStart(6, FY) >= Convert.ToDateTime(((DateTime)original_entity["apd_ibg_effectivedate"]).ToShortDateString()))
                        {
                            if (jun_total == 0)
                            {
                                isValid = false;
                                _months += " June, ";
                            }
                        }
                    }

                }

            return isValid;

            #endregion
        }

        protected DateTime ReturnMonthStart(int month, string fiscalyear)
        {
            if (month > 6)
                return Convert.ToDateTime(Convert.ToDateTime(month.ToString() + "/1/" + fiscalyear.Substring(0, 4)).ToShortDateString());
            else
                return Convert.ToDateTime(Convert.ToDateTime(month.ToString() + "/1/" + fiscalyear.Substring(5, 4)).ToShortDateString());
        }
    }
}