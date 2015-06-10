function Form_onload(ExecutionObj)
{
	//debugger;
	
	 var Fields = ["apd_ibg_yearlybudget","apd_ibg_yearlybudget_sym","apd_ibg_budgetedamount_sym","apd_ibg_budgetedamount",
     "apd_ibg_flexiblespendingamount_sym","apd_ibg_flexiblespendingamount","apd_ibg_emergencyreserveamount_sym","apd_ibg_emergencyreserveamount",
     "apd_ibg_budgetedflexiblespendingamount_sym","apd_ibg_budgetedflexiblespendingamount","apd_ibg_budgetedemergencyreserveamount_sym",
     "apd_ibg_budgetedemergencyreserveamount","apd_ibg_balanceamount_sym","apd_ibg_balanceamount","apd_ibg_balanceflexiblespendingamount_sym",
     "apd_ibg_balanceflexiblespendingamount","apd_ibg_balanceemergencyreserveamount_sym","apd_ibg_balanceemergencyreserveamount",
     "apd_ibg_allocatedamount_sym","apd_ibg_allocatedamount","apd_ibg_totalbudgetedamount_sym","apd_ibg_totalbudgetedamount",
     "apd_ibg_remainingbalanceamount_sym","apd_ibg_remainingbalanceamount", "apd_ibg_tiercode"];
    FormatTextFields(Fields);
    
    var Fields = ["apd_reasonforareareview", "apd_reasonforcentralreview", "apd_consumerdoesnotacceptcostplanexplanation"];
    FormatTextAreaFields(Fields);
    
	window.CrmFormType = {
						Create: 1,
						Edit: 2,
						Readonly: 3,
						Disabled: 4
                      }
     window.CrmFormStatus ={
						Draft: 1,
						Historical: 2,
						PendingReview: 3,
						CurrentApproved: 4
						};
	window.CrmFormProcessingStatus ={
								None: 1,
								PendingWSCReview: 2,
								PendingAreaOfficeReview: 3,
								PendingCentralOfficeReview: 4,
								Approved: 5,
								Denied: 6
								};
	
	window._processingstatus = Xrm.Page.getAttribute("apd_ibg_processingstatuscode").getValue();
    window._statuscode = Xrm.Page.getAttribute("statuscode").getValue();

    var current_ibg_balanceamount = Xrm.Page.getAttribute("apd_ibg_balanceamount").getValue() == null ? "" : Xrm.Page.getAttribute("apd_ibg_balanceamount").getValue();
    var current_ibg_balanceflexiblespendingamount = Xrm.Page.getAttribute("apd_ibg_balanceflexiblespendingamount").getValue() == null ? "" : Xrm.Page.getAttribute("apd_ibg_balanceflexiblespendingamount").getValue();
    var current_ibg_balanceemergencyreserveamount = Xrm.Page.getAttribute("apd_ibg_balanceemergencyreserveamount").getValue() == null ? "" : Xrm.Page.getAttribute("apd_ibg_balanceemergencyreserveamount").getValue();
    var current_ibg_balancesupplementalfundamount = Xrm.Page.getAttribute("apd_ibg_balancesupplementalfundamount").getValue() == null ? "" : Xrm.Page.getAttribute("apd_ibg_balancesupplementalfundamount").getValue();

    // if supplemental fund amount is zero then hide the supplemental fund section
    if (Xrm.Page.getAttribute("apd_ibg_supplementalfundamount").getValue() == null && Xrm.Page.getAttribute("apd_ibg_budgetedsupplementalfundamount").getValue() == null && Xrm.Page.getAttribute("apd_ibg_balancesupplementalfundamount").getValue() == null)
        HideSection("Annual Cost Plan", "Supplemental Fund");
    else if (Xrm.Page.getAttribute("apd_ibg_supplementalfundamount").getValue() == 0 && Xrm.Page.getAttribute("apd_ibg_budgetedsupplementalfundamount").getValue() == 0 && Xrm.Page.getAttribute("apd_ibg_balancesupplementalfundamount").getValue() == 0)
        HideSection("Annual Cost Plan", "Supplemental Fund");
else
        ShowSection("Annual Cost Plan", "Supplemental Fund");

    if (Xrm.Page.getAttribute("apd_consumerdoesnotacceptcostplanexplanation").getValue() == null || Xrm.Page.getAttribute("apd_consumerdoesnotacceptcostplanexplanation").getValue() == "")
        Xrm.Page.getControl("apd_consumerdoesnotacceptcostplanexplanation").setDisabled(true);

    if ((_statuscode == CrmFormStatus.PendingReview && (_processingstatus == CrmFormProcessingStatus.PendingAreaOfficeReview
    || _processingstatus == CrmFormProcessingStatus.PendingCentralOfficeReview)) || _statuscode == CrmFormStatus.CurrentApproved) {
        Xrm.Page.getControl("apd_consumerdoesnotacceptcostplanexplanation").setDisabled(true);
    }
    
    var wscprocessingcomments = ["apd_ibg_wscprocessingcomments"];
    var areaprocessingcomments = ["apd_ibg_areaofficeprocessingcomments"];
    var centralprocessingcomments = ["apd_ibg_centralofficeprocessingcomments"];

	switch(Xrm.Page.ui.getFormType())
	{
	case CrmFormType.Create:
						HandleProcessingComments(_statuscode, _processingstatus);
						Xrm.Page.ui.tabs.get("Notes").setVisible(false);
						break;
	case CrmFormType.Edit:
						setInterval(ReloadHandler, 30000);
						Xrm.Page.getControl("apd_consumerid").setDisabled(true);
						Xrm.Page.getControl("apd_fiscalyearid").setDisabled(true);
						Xrm.Page.getControl("apd_ibg_effectivedate").setDisabled(true);
						HandleActiveInactiveFields(_statuscode, _processingstatus);
						switch (_statuscode) {
									case CrmFormStatus.Draft:
										HandleProcessingComments(_statuscode, _processingstatus);
										break;
									case CrmFormStatus.Historical:
									case CrmFormStatus.CurrentApproved:
										DisableAllFormFields(true);
										
										FormatTextAreaFields(wscprocessingcomments);
							            FormatTextAreaFields(areaprocessingcomments);
							            FormatTextAreaFields(centralprocessingcomments);
										break;
									default:
										HandleProcessingComments(_statuscode, _processingstatus);
								}
						break;
	
	}	
}

function HandleActiveInactiveFields(_statuscode, _processingstatus) {

        if (_statuscode == CrmFormStatus.Draft && _processingstatus == CrmFormProcessingStatus.None)
            MakeTheFollowingFieldsRequired(["apd_consumerid", "apd_fiscalyearid"], "required");

        if (_statuscode == CrmFormStatus.PendingReview && _processingstatus == CrmFormProcessingStatus.PendingWSCReview) {

            MakeTheFollowingFieldsRequired(["apd_ibg_effectivedate", "apd_ibg_wscprocessingcomments"], "required");
        }
        if (_statuscode == CrmFormStatus.PendingReview && _processingstatus == CrmFormProcessingStatus.PendingAreaOfficeReview) {

            MakeTheFollowingFieldsRequired(["apd_ibg_effectivedate", "apd_ibg_areaofficeprocessingcomments"], "required");
        }
        if (_statuscode == CrmFormStatus.PendingReview && _processingstatus == CrmFormProcessingStatus.PendingCentralOfficeReview) {

            MakeTheFollowingFieldsRequired(["apd_ibg_effectivedate", "apd_ibg_centralofficeprocessingcomments"], "required");
        }
}

function HandleProcessingComments(_statuscode, _processingstatus) {
        
        var wscprocessingcomments = ["apd_ibg_wscprocessingcomments"];
        var areaprocessingcomments = ["apd_ibg_areaofficeprocessingcomments"];
        var centralprocessingcomments = ["apd_ibg_centralofficeprocessingcomments"];
        
        var wscprocessingfields = ["apd_processedbywscid", "apd_ibg_wscprocesseddate"];
        var areaprocessingfields = ["apd_processedbyareaofficeuserid", "apd_ibg_areaofficeprocesseddate"];
        var centralprocessingfields = ["apd_processedbycentralofficeuserid", "apd_ibg_centralofficeprocesseddate"];
            
    if (_statuscode == CrmFormStatus.PendingReview &&
		    _processingstatus == CrmFormProcessingStatus.PendingWSCReview && ((UserHasRole("iBudget WSC") || UserHasRole("iBudget WSC Manager")) || UserHasRole("iBudget Area Office Staff") || UserHasRole("iBudget Central Office Staff"))) {
            //SectionDisable("WSC Processing", false);
            DisableFollowingFields(wscprocessingfields);
            DisableFollowingFields(areaprocessingfields);
            DisableFollowingFields(centralprocessingfields);
            
            DisableFollowingFields(areaprocessingcomments);
            DisableFollowingFields(centralprocessingcomments);
            
            FormatTextAreaFields(areaprocessingcomments);
            FormatTextAreaFields(centralprocessingcomments);
            
            HideSection("Annual Cost Plan", "State Office Processing")
        }
        else if (_statuscode == CrmFormStatus.PendingReview &&
		   _processingstatus == CrmFormProcessingStatus.PendingAreaOfficeReview && (UserHasRole("iBudget Area Office Staff") || UserHasRole("iBudget Central Office Staff"))) {
            DisableFollowingFields(wscprocessingfields);
            DisableFollowingFields(areaprocessingfields);
            DisableFollowingFields(centralprocessingfields);
            
            DisableFollowingFields(wscprocessingcomments);
            DisableFollowingFields(centralprocessingcomments);
            
            FormatTextAreaFields(wscprocessingcomments);
            FormatTextAreaFields(centralprocessingcomments);
           
            }
        else if (_statuscode == CrmFormStatus.PendingReview &&
		    _processingstatus == CrmFormProcessingStatus.PendingCentralOfficeReview && UserHasRole("iBudget Central Office Staff")) {
            DisableFollowingFields(wscprocessingfields);
            DisableFollowingFields(areaprocessingfields);
            DisableFollowingFields(centralprocessingfields);

            DisableFollowingFields(wscprocessingcomments);
            DisableFollowingFields(areaprocessingcomments);
            
            FormatTextAreaFields(wscprocessingcomments);
            FormatTextAreaFields(areaprocessingcomments);
        }
        else if (_statuscode == CrmFormStatus.Draft) {
            HideSection("Annual Cost Plan", "WSC Processing");
            HideSection("Annual Cost Plan", "Area Office Processing");
            HideSection("Annual Cost Plan", "Area Office Processing 1");
            HideSection("Annual Cost Plan", "State Office Processing");
        }
        else {
            DisableAllFormFields(true);
            
            FormatTextAreaFields(wscprocessingcomments);
            FormatTextAreaFields(areaprocessingcomments);
            FormatTextAreaFields(centralprocessingcomments);
            }
    }

function ReloadHandler() {
        
        var ODataQuery = "/APD_IBG_annualcostplanSet?$select=APD_ibg_RemainingBalanceAmount&$filter=APD_IBG_annualcostplanId eq guid'"+Xrm.Page.data.entity.getId()+"'";
	
	    var context = Xrm.Page.context;
	    var serverUrl = document.location.protocol + "//" + document.location.host + "/" + context.getOrgUniqueName();
	
	    var ODataURL = serverUrl + "/XRMServices/2011/OrganizationData.svc/" + ODataQuery;
	    //var ODataURL = 'http://ns-apd-wvpap02:5555/APD/XRMServices/2011/OrganizationData.svc' + ODataQuery; //serverUrl + "/XRMServices/2011/OrganizationData.svc"; 
//alert(ODataURL);
	    $.ajax({
	        type: "GET",
	        contentType: "application/json; charset=utf-8",
	        datatype: "json",
	        url: ODataURL,
	        beforeSend: function(XMLHttpRequest) {
	            XMLHttpRequest.setRequestHeader("Accept", "application/json");
	        },
	        success: function(data, textStatus, XmlHttpRequest) {
	            // Handle result from successful execution
	            var r = data.d.results[0];
	            
			    var latest_APD_ibg_RemainingBalanceAmount = parseFloat(eval(r.APD_ibg_RemainingBalanceAmount.Value));
			    
			    var form_APD_ibg_RemainingBalanceAmount = Xrm.Page.getAttribute("apd_ibg_remainingbalanceamount").getValue();
				
		       	if (latest_APD_ibg_RemainingBalanceAmount != form_APD_ibg_RemainingBalanceAmount)
		       		location.reload();		        
	        },
	        error: function(XmlHttpRequest, textStatus, errorObject) {
	            // Handle result from unsuccessful execution
	            alert("OData Execution Error Occurred - 1");
	        }
	    });
}

function Form_onsave()
{		
	 var arrFields =   ["statuscode", "apd_fiscalyearid", "apd_consumerid", "apd_ibg_processingstatuscode", "apd_ibg_annualbudgetid", "apd_ibg_allocatedamount", "apd_ibg_yearlybudget", "apd_ibg_flexiblespendingamount", "apd_ibg_emergencyreserveamount", "apd_consumeracceptscostplan","apd_consumerdoesnotacceptcostplanexplanation","apd_reasonforareareview", "apd_ibg_iscopiedplan","apd_ibg_iscopybuttonclicked","apd_ibg_submitteddate","apd_processedbywscid","apd_ibg_wscprocesseddate","apd_ibg_wscprocessingcomments","apd_processedbyareaofficeuserid","apd_ibg_areaofficeprocesseddate","apd_ibg_areaofficeprocessingcomments","apd_processedbycentralofficeuserid","apd_ibg_centralofficeprocesseddate","apd_ibg_centralofficeprocessingcomments", "apd_ibg_effectivedate", "apd_reasonforcentralreview", "apd_ibg_isnextfycopybuttonclicked"];
	 ForceFieldsToSubmit(arrFields);
}

function submit_onclick()
{
	if(confirm('Are you sure you want to submit this cost plan?'))
	{
	Xrm.Page.getAttribute("statuscode").setValue(3);
	Xrm.Page.getAttribute("apd_ibg_processingstatuscode").setValue(2);
	Xrm.Page.getAttribute("apd_ibg_submitteddate").setValue(new Date());
	Xrm.Page.data.entity.save();
	}
}

function aoreview_onclick()
{
	if(confirm('Are you sure you want to send this cost plan for area review?'))
	{
	AreaReviewHandler();
	}
}

function WSCsendback_onclick()
{
	if(confirm('Are you sure you want to send this cost plan back to WSC?'))
	{
	SendBackToWSCHandler();
	}
}

function coreview_onclick()
{
	if(confirm('Are you sure you want to send this cost plan for central review?'))
	{
	CentralOfficeReviewHandler();
	}
}

function saveandprocess_onclick()
{
	if(confirm('Are you sure you want to process this cost plan?'))
	{
		ProcessHandler(_statuscode, _processingstatus);
	}
}

function ProcessHandler(_statuscode, _processingstatus) {
    
	var _returnvalue;

	if (_statuscode == 3 && _processingstatus == 2) {
			_returnvalue = WSCDetails(GetUserId(), GetUserFullName());
	}
	if (_statuscode == 3 && _processingstatus == 3) {
		_returnvalue = AreaOfficeDetails(GetUserId(), GetUserFullName());
	}
	if (_statuscode == 3 && _processingstatus == 4) {
		if (Xrm.Page.getAttribute("apd_ibg_balancesupplementalfundamount").getValue() < 0) 
			{
			alert('The cost plan cannot be processed as the total allocated amount is less than the total budgeted cost plan amount.')
			ExecutionObj.getEventArgs().preventDefault();                
			}
		else 
			{
			_returnvalue = CentralOfficeDetails(GetUserId(), GetUserFullName());
			}
	}
	if (_returnvalue == true) {
		Xrm.Page.getAttribute("statuscode").setValue(4);
		Xrm.Page.getAttribute("apd_ibg_processingstatuscode").setValue(5);
		Xrm.Page.data.entity.save("saveandclose");
	}
}

function WSCDetails(userid, userfullname) {

	if (Xrm.Page.getAttribute("apd_ibg_wscprocessingcomments").getValue() == null) {
		alert("Please enter WSC processing comments and \nClick Save and then \nClick Process.");
		Xrm.Page.getControl("apd_ibg_wscprocessingcomments").setFocus(true);
		return false;
	}
	if (Xrm.Page.getAttribute("apd_consumeracceptscostplan").getValue() == false && (Xrm.Page.getAttribute("apd_consumerdoesnotacceptcostplanexplanation").getValue() == null || Xrm.Page.getAttribute("apd_consumerdoesnotacceptcostplanexplanation").getValue() == "")) {
		alert("Please provide an explanation of why the Consumer has not accepted the cost plan or select Yes for Consumer Accepts Cost Plan.");
		Xrm.Page.getControl("apd_consumerdoesnotacceptcostplanexplanation").setDisabled(false);
		Xrm.Page.getControl("apd_consumerdoesnotacceptcostplanexplanation").setFocus(true);
		return false;
	}
	if (Xrm.Page.getAttribute("apd_ibg_effectivedate").getValue() == null) {
		alert("Please enter an effective date for this cost plan.");
		Xrm.Page.getControl("apd_ibg_effectivedate").setFocus(true);
		return false;
	}

	var arrUser = new Array();
	var userObj = new Object;
	userObj.id = userid;
	userObj.name = userfullname;
	userObj.entityType = 'systemuser';
	arrUser[0] = userObj;

	Xrm.Page.getAttribute("apd_processedbywscid").setValue(arrUser);
	Xrm.Page.getAttribute("apd_ibg_wscprocesseddate").setValue(new Date());
	return true;
}

function AreaOfficeDetails(userid, userfullname) {

	if (Xrm.Page.getAttribute("apd_ibg_effectivedate").getValue() == null) {
		alert("Please enter an effective date for this cost plan.");
		Xrm.Page.getControl("apd_ibg_effectivedate").setFocus(true);
		return false;
	}
	if (Xrm.Page.getAttribute("apd_ibg_areaofficeprocessingcomments").getValue() == null) {
		alert("Please enter Area Office Processing comments and \nClick Save and then \nClick Process.");
		Xrm.Page.getControl("apd_ibg_areaofficeprocessingcomments").setFocus(true);
		return false;
	}
	var arrUser = new Array();
	var userObj = new Object;
	userObj.id = userid;
	userObj.name = userfullname;
	userObj.entityType = 'systemuser';
	arrUser[0] = userObj;

	Xrm.Page.getAttribute("apd_processedbyareaofficeuserid").setValue(arrUser);
	Xrm.Page.getAttribute("apd_ibg_areaofficeprocesseddate").setValue(new Date());
	return true;

}

function CentralOfficeDetails(userid, userfullname) {

	if (Xrm.Page.getAttribute("apd_ibg_effectivedate").getValue() == null) {
		alert("Please enter an effective date for this cost plan.");
		Xrm.Page.getControl("apd_ibg_effectivedate").setFocus(true);
		return false;
	}
	if (Xrm.Page.getAttribute("apd_ibg_centralofficeprocessingcomments").getValue() == null) {
		alert("Please enter Central Office Processing comments and \nClick Save and then \nClick Process.");
		Xrm.Page.getControl("apd_ibg_centralofficeprocessingcomments").setFocus(true);
		return false;
	}

	var arrUser = new Array();
	var userObj = new Object;
	userObj.id = userid;
	userObj.name = userfullname;
	userObj.entityType = 'systemuser';
	arrUser[0] = userObj;

	Xrm.Page.getAttribute("apd_processedbycentralofficeuserid").setValue(arrUser);
	Xrm.Page.getAttribute("apd_ibg_centralofficeprocesseddate").setValue(new Date());
	return true;

}

function AreaReviewHandler() {
	var _returnvalue;

	_returnvalue = WSCDetails(GetUserId(), GetUserFullName());
	
	if (Xrm.Page.getAttribute("apd_consumeracceptscostplan").getValue() == false && (Xrm.Page.getAttribute("apd_consumerdoesnotacceptcostplanexplanation").getValue() == null || Xrm.Page.getAttribute("apd_consumerdoesnotacceptcostplanexplanation").getValue() == "")) {
		alert("Please provide an explanation of why the Consumer has not accepted the cost plan or select Yes for Consumer Accepts Cost Plan.");
		Xrm.Page.getControl("apd_consumerdoesnotacceptcostplanexplanation").setDisabled(false);
		Xrm.Page.getControl("apd_consumerdoesnotacceptcostplanexplanation").setFocus(true);
		return false;
	}
	else {
		var arrUser = new Array();
		var userObj = new Object;
		userObj.id = GetUserId();
		userObj.name = GetUserFullName();
		userObj.entityType = 'systemuser';
		arrUser[0] = userObj;

		Xrm.Page.getAttribute("apd_processedbywscid").setValue(arrUser);
		Xrm.Page.getAttribute("apd_ibg_wscprocesseddate").setValue(new Date());
		
		var d = new Date();
    
    	Xrm.Page.getAttribute("apd_reasonforareareview").setValue(d.toLocaleDateString() + " " + d.toLocaleTimeString() + ' - Manually sent to Area Office Review');
		Xrm.Page.getAttribute("statuscode").setValue(3);
		Xrm.Page.getAttribute("apd_ibg_processingstatuscode").setValue(3);
		Xrm.Page.data.entity.save("saveandclose");
	}
}

function SendBackToWSCHandler() {

  	processingstatus = Xrm.Page.getAttribute("apd_ibg_processingstatuscode").getSelectedOption().value;
  	
  	if(processingstatus == 3)
  	{
  		if (Xrm.Page.getAttribute("apd_ibg_areaofficeprocessingcomments").getValue() == null) {
			alert("Please enter Area Office Processing comments.");
			Xrm.Page.getControl("apd_ibg_areaofficeprocessingcomments").setFocus(true);
			return;
		}
		else
		{
			var arrUser = new Array();
			var userObj = new Object;
			userObj.id = GetUserId();
			userObj.name = GetUserFullName();
			userObj.entityType = 'systemuser';
			arrUser[0] = userObj;
		
			Xrm.Page.getAttribute("apd_processedbyareaofficeuserid").setValue(arrUser);
			Xrm.Page.getAttribute("apd_ibg_areaofficeprocesseddate").setValue(new Date());
		
			Xrm.Page.getAttribute("statuscode").setValue(3);
			Xrm.Page.getAttribute("apd_ibg_processingstatuscode").setValue(2);
			Xrm.Page.data.entity.save("saveandclose");
		}
	}
	else if(processingstatus == 4)
	{
  		if (Xrm.Page.getAttribute("apd_ibg_centralofficeprocessingcomments").getValue() == null) {
			alert("Please enter Central Office Processing comments.");
			Xrm.Page.getControl("apd_ibg_centralofficeprocessingcomments").setFocus(true);
			return;			
		}
		else
		{
			var arrUser = new Array();
			var userObj = new Object;
			userObj.id = GetUserId();
			userObj.name = GetUserFullName();
			userObj.entityType = 'systemuser';
			arrUser[0] = userObj;
		
			Xrm.Page.getAttribute("apd_processedbyareaofficeuserid").setValue(arrUser);
			Xrm.Page.getAttribute("apd_ibg_areaofficeprocesseddate").setValue(new Date());
		
			Xrm.Page.getAttribute("statuscode").setValue(3);
			Xrm.Page.getAttribute("apd_ibg_processingstatuscode").setValue(2);
			Xrm.Page.data.entity.save("saveandclose");
		}
	}
}


function CentralOfficeReviewHandler() {
	if (Xrm.Page.getAttribute("apd_ibg_areaofficeprocessingcomments").getValue() == null) {
		alert("Please enter Area Office Processing comments.");
		Xrm.Page.getControl("apd_ibg_areaofficeprocessingcomments").setFocus(true);
		return;
	}

	var arrUser = new Array();
	var userObj = new Object;
	userObj.id = GetUserId();
	userObj.name = GetUserFullName();
	userObj.entityType = 'systemuser';
	arrUser[0] = userObj;

	Xrm.Page.getAttribute("apd_processedbyareaofficeuserid").setValue(arrUser);
	Xrm.Page.getAttribute("apd_ibg_areaofficeprocesseddate").setValue(new Date());

	Xrm.Page.getAttribute("statuscode").setValue(3);
	Xrm.Page.getAttribute("apd_ibg_processingstatuscode").setValue(4);
	Xrm.Page.data.entity.save("saveandclose");
}

function SendBackToAreaOfficeReviewHandler() {
	if (Xrm.Page.getAttribute("apd_ibg_centralofficeprocessingcomments").getValue() == null) {
		alert("Please enter Central Office Processing comments.");
		Xrm.Page.getControl("apd_ibg_centralofficeprocessingcomments").setFocus(true);
		return;
	}

	var arrUser = new Array();
	var userObj = new Object;
	userObj.id = GetUserId();
	userObj.name = GetUserFullName();
	userObj.entityType = 'systemuser';
	arrUser[0] = userObj;

	Xrm.Page.getAttribute("apd_processedbycentralofficeuserid").setValue(arrUser);
	Xrm.Page.getAttribute("apd_ibg_centralofficeprocesseddate").setValue(new Date());

	Xrm.Page.getAttribute("statuscode").setValue(3);
	Xrm.Page.getAttribute("apd_ibg_processingstatuscode").setValue(3);
	Xrm.Page.data.entity.save("saveandclose");	
}

function copynextfy_onclick()
{
	if(confirm('Are you sure you want to copy this cost plan to next FY?'))
	{
	Xrm.Page.getAttribute("apd_ibg_isnextfycopybuttonclicked").setValue(true);
	Xrm.Page.data.entity.save('saveandclose');
	}
}	

function copy_onclick()
{
	if(confirm('Are you sure you want to copy this cost plan?'))
	{
	Xrm.Page.getAttribute("apd_ibg_iscopybuttonclicked").setValue(true);
	Xrm.Page.data.entity.save('saveandclose');
	}
}

function aosendback_onclick()
{
	if(confirm('Are you sure you want to send this cost plan back to area?'))
	{
	SendBackToAreaOfficeReviewHandler();
	}
}

//handling Submit Button
function handlesubmit_button() 
{
 IsUserWSC = UserHasRole("iBudget WSC") || UserHasRole("iBudget WSC Manager");
 IsUserAO = UserHasRole("iBudget Area Office Staff");
 IsUserCO = UserHasRole("iBudget Central Office Staff");
 IsUserSA = UserHasRole("System Administrator");

  statuscode = Xrm.Page.getAttribute("statuscode").getSelectedOption().value;
  processingstatus = Xrm.Page.getAttribute("apd_ibg_processingstatuscode").getSelectedOption().value;

  if(Xrm.Page.ui.getFormType() == 1)
  return false;
  if(statuscode == 1 && processingstatus == 1 && (IsUserWSC || IsUserAO || IsUserCO ))
  return true;
  else
  return false;

}

//handling Area Review Button
function handleareareview_button()
{

 IsUserWSC = UserHasRole("iBudget WSC") || UserHasRole("iBudget WSC Manager");
 IsUserAO = UserHasRole("iBudget Area Office Staff");
 IsUserCO = UserHasRole("iBudget Central Office Staff");
 IsUserSA = UserHasRole("System Administrator");

  statuscode = Xrm.Page.getAttribute("statuscode").getSelectedOption().value;
  processingstatus = Xrm.Page.getAttribute("apd_ibg_processingstatuscode").getSelectedOption().value;
 
  if(statuscode == 3 && processingstatus == 2 && (IsUserWSC || IsUserAO || IsUserCO ))
  return true;
  else
  return false;
}

//handling Send back to WSC Button
function handlesendbackWSC_button() 
{

 IsUserWSC = UserHasRole("iBudget WSC") || UserHasRole("iBudget WSC Manager");
 IsUserAO = UserHasRole("iBudget Area Office Staff");
 IsUserCO = UserHasRole("iBudget Central Office Staff");
 IsUserSA = UserHasRole("System Administrator");

  statuscode = Xrm.Page.getAttribute("statuscode").getSelectedOption().value;
  processingstatus = Xrm.Page.getAttribute("apd_ibg_processingstatuscode").getSelectedOption().value;
  
  if(statuscode == 3 && (processingstatus == 3 || processingstatus == 4 ) && ( IsUserAO || IsUserCO ))
  return true;
  else
  return false;
}

//handling Central Office Review Button
function handlecentralreview_button() 
{

 IsUserWSC = UserHasRole("iBudget WSC") || UserHasRole("iBudget WSC Manager");
 IsUserAO = UserHasRole("iBudget Area Office Staff");
 IsUserCO = UserHasRole("iBudget Central Office Staff");
 IsUserSA = UserHasRole("System Administrator");

  statuscode = Xrm.Page.getAttribute("statuscode").getSelectedOption().value;
  processingstatus = Xrm.Page.getAttribute("apd_ibg_processingstatuscode").getSelectedOption().value;
  
  if(statuscode == 3 && processingstatus == 3 && ( IsUserAO || IsUserCO ))
  return true;
  else
  return false;
}

//handling Save and Process Button
function handlesaveandprocess_button() 
{

 IsUserWSC = UserHasRole("iBudget WSC") || UserHasRole("iBudget WSC Manager");
 IsUserAO = UserHasRole("iBudget Area Office Staff");
 IsUserCO = UserHasRole("iBudget Central Office Staff");
 IsUserSA = UserHasRole("System Administrator");

  statuscode = Xrm.Page.getAttribute("statuscode").getSelectedOption().value;
  processingstatus = Xrm.Page.getAttribute("apd_ibg_processingstatuscode").getSelectedOption().value;

//alert(IsUserWSC + ':' + IsUserAO + ':' + IsUserCO + ':' + IsUserSA + ':' + statuscode + ':' + processingstatus);

  if(statuscode == 3 && processingstatus == 2 && (IsUserWSC || IsUserAO || IsUserCO))
  return true;
  else if(statuscode == 3 && processingstatus == 3 && (IsUserAO || IsUserCO))
  return true;
  else if(statuscode == 3 && processingstatus == 4 && IsUserCO)
  return true;
  else
  return false;
}

function handlecopycpnextfy_button()
{
  IsUserWSC = UserHasRole("iBudget WSC") || UserHasRole("iBudget WSC Manager");
  IsUserAO = UserHasRole("iBudget Area Office Staff");
  IsUserCO = UserHasRole("iBudget Central Office Staff");
  IsUserSA = UserHasRole("System Administrator");

  statuscode = Xrm.Page.getAttribute("statuscode").getSelectedOption().value;
  processingstatus = Xrm.Page.getAttribute("apd_ibg_processingstatuscode").getSelectedOption().value;
  
  //alert(IsUserWSC + ':' + IsUserAO + ':' + IsUserCO + ':' + IsUserSA + ':' + statuscode + ':' + processingstatus +  ':' + Xrm.Page.getAttribute("apd_ibg_isnextfycopybuttonclicked").getValue());
  
  if(statuscode == 4 && processingstatus == 5 && (IsUserWSC || IsUserAO || IsUserCO ) && Xrm.Page.getAttribute("apd_ibg_isnextfycopybuttonclicked").getValue() == false)
  	return true;
  else
  	return false;
}

function handlecopycp_button()
{
  IsUserWSC = UserHasRole("iBudget WSC") || UserHasRole("iBudget WSC Manager");
  IsUserAO = UserHasRole("iBudget Area Office Staff");
  IsUserCO = UserHasRole("iBudget Central Office Staff");
  IsUserSA = UserHasRole("System Administrator");

  statuscode = Xrm.Page.getAttribute("statuscode").getSelectedOption().value;
  processingstatus = Xrm.Page.getAttribute("apd_ibg_processingstatuscode").getSelectedOption().value;
  
  if(statuscode == 4 && processingstatus == 5 && (IsUserWSC || IsUserAO || IsUserCO ) && Xrm.Page.getAttribute("apd_ibg_iscopybuttonclicked").getValue() == false)
  return true;
  else
  return false;
}

//handling Send back to AO Button
function handlesendbackAO_button() 
{

 IsUserWSC = UserHasRole("iBudget WSC") || UserHasRole("iBudget WSC Manager");
 IsUserAO = UserHasRole("iBudget Area Office Staff");
 IsUserCO = UserHasRole("iBudget Central Office Staff");
 IsUserSA = UserHasRole("System Administrator");

  statuscode = Xrm.Page.getAttribute("statuscode").getSelectedOption().value;
  processingstatus = Xrm.Page.getAttribute("apd_ibg_processingstatuscode").getSelectedOption().value;
  
  if(statuscode == 3 && processingstatus == 4 && ( IsUserCO ))
  return true;
  else
  return false;
}

function apd_consumeracceptscostplan_onchange()
{
	if(Xrm.Page.getAttribute("apd_consumeracceptscostplan").getValue() == false)
	{
		Xrm.Page.getControl("apd_consumerdoesnotacceptcostplanexplanation").setDisabled(false);
		MakeTheFollowingFieldsRequired(["apd_consumerdoesnotacceptcostplanexplanation"], "required");
		Xrm.Page.getControl("apd_consumerdoesnotacceptcostplanexplanation").setFocus(true);
	}
	else
	{
		Xrm.Page.getAttribute("apd_consumerdoesnotacceptcostplanexplanation").setValue(null);
		Xrm.Page.getControl("apd_consumerdoesnotacceptcostplanexplanation").setDisabled(true);
		MakeTheFollowingFieldsRequired(["apd_consumerdoesnotacceptcostplanexplanation"], "none");
	}	
}