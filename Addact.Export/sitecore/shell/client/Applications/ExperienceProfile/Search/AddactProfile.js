
var Addact = {
    ExportData: function () {
        var apiurl = "/api/AddactExportData/ExportProfile";
        var query = "";
        var fromdate = $("[data-sc-id=FromDatePick]").val();
        var todate = $("[data-sc-id=ToDatePick]").val();
        if (fromdate != undefined && fromdate != "") {
            query += "?startDate=" + fromdate;

        }
        if (todate != undefined && fromdate != "") {
            if (query != "") {
                query += "&endDate=" + todate;
            }
            else { query += "?endDate=" + todate; }
        }
        window.location.assign(apiurl + query);
    },
    initialized: function () {
        var buttonhtml = '<button data-sc-id="ExportProfile" class="btn sc-button btn-default noText sc_Button_68 data-sc-registered" title="Export Profile Data" click="javascript:Addact.ExportData()"  type="button">';
        buttonhtml += '<div class="sc-icon data-sc-registered" style="background-position: center center; background-image: url(&quot;/sitecore/shell/themes/standard/~/icon/Office/16x16/box_out.png&quot;);"></div>';
        buttonhtml += '<span class="sc-button-text data-sc-registered" data-bind="text: text"></span></button>';
        if ($(".sc-applicationHeader-contextSwitcher").length > 0) {
            alert('find');
            $(".sc-applicationHeader-contextSwitcher").html(buttonhtml);

        }


    },

}


$(function () { Addact.initialized(); });