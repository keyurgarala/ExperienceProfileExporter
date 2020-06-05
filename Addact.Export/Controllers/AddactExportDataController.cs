using Addact.Export.Models;
using Sitecore;
using Sitecore.DependencyInjection;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Marketing.Definitions;
using Sitecore.XConnect;
using Sitecore.XConnect.Client;
using Sitecore.XConnect.Client.Configuration;
using Sitecore.XConnect.Collection.Model;
using Sitecore.XConnect.Operations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace Addact.Export.Controllers
{
    public class AddactExportDataController : Controller
    {
        public FileContentResult ExportProfile(string startDate, string endDate)
        {
            try
            {

                List<ExperienceProfileDetail> export = new List<ExperienceProfileDetail>();
                string[] paramiters = { WebVisit.DefaultFacetKey, LocaleInfo.DefaultFacetKey, IpInfo.DefaultFacetKey };
                var interactionsobj = new RelatedInteractionsExpandOptions(paramiters);
                DateTime sdate = DateTime.Now;
                DateTime edate = DateTime.Now;

                if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out sdate))
                    interactionsobj.StartDateTime = sdate.AddDays(1).ToUniversalTime();
                else
                    interactionsobj.StartDateTime = DateTime.UtcNow.AddDays(-30);
                if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out edate))
                    interactionsobj.EndDateTime = edate.AddDays(1).ToUniversalTime();
                else
                    interactionsobj.EndDateTime = DateTime.UtcNow;
                interactionsobj.Limit = int.MaxValue;
                ExportDataResult exportResult;
                using (Sitecore.XConnect.Client.XConnectClient client = SitecoreXConnectClientConfiguration.GetClient())
                {
                    List<Contact> contactList = new List<Contact>();
                    bool includeAnonymous = false;
                    var settingvalue = Sitecore.Configuration.Settings.GetSetting("IncludeAnonymous");
                    bool.TryParse(settingvalue, out includeAnonymous);
                    var Contactsid = client.Contacts.Where(d => d.Interactions.Any(i => i.StartDateTime >= interactionsobj.StartDateTime && i.EndDateTime <= interactionsobj.EndDateTime)).AsAsyncQueryable();
                    if (!includeAnonymous)
                        Contactsid = Contactsid.Where(c => c.Identifiers.Any(t => t.IdentifierType == Sitecore.XConnect.ContactIdentifierType.Known));

                    contactList = Contactsid.ToList().Result;
                    var references = new List<IEntityReference<Sitecore.XConnect.Contact>>();
                    references.AddRange(contactList);
                    var contacts = client.Get<Contact>(references, new Sitecore.XConnect.ContactExpandOptions(PersonalInformation.DefaultFacetKey)
                    {
                        Interactions = interactionsobj
                    }.Expand<EmailAddressList>().Expand<AddressList>().Expand<PhoneNumberList>());
                    exportResult = new ExportDataResult()
                    {
                        Content = GenerateFileContent(contacts),
                        FileName = GenerateFileName(interactionsobj.StartDateTime.Value, interactionsobj.EndDateTime.Value),
                        MediaType = "application/octet-stream"
                    };

                }

                FileContentResult fileresult;
                if (exportResult != null)
                {
                    fileresult = new FileContentResult(exportResult.Content, exportResult.MediaType);
                    fileresult.FileDownloadName = exportResult.FileName;
                }
                else
                { fileresult = new FileContentResult(null, "application/octet-stream") { FileDownloadName = "NoData.csv" }; }
                return fileresult;
            }
            catch (Exception ex)
            {
                Log.Error("ERROR IN EXPORT PROFILE GETDATA:", ex.Message);
                return new FileContentResult(null, "application/octet-stream") { FileDownloadName = "NoData.csv" };
            }
        }



        protected virtual string GenerateFileName(DateTime? startDate, DateTime? endDate)
        {
            string str = string.Empty;
            if (startDate.HasValue)
                str = str + "_from_" + DateUtil.ToIsoDate(startDate.Value);
            if (endDate.HasValue)
                str = str + "_until_" + DateUtil.ToIsoDate(endDate.Value);
            if (string.IsNullOrEmpty(str))
                str = "-" + DateUtil.IsoNow;
            return FormattableString.Invariant(FormattableStringFactory.Create("Profile-Data{0}.csv", (object)str));
        }
        protected byte[] GenerateFileContent(IReadOnlyCollection<IEntityLookupResult<Contact>> contacts)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string[] fieldColumnsList = {"Site Name",
                "FirstName",
                "MiddleName",
                "LastName",
                "Nickname",
                "Gender",
                "Email",
                "Phone Number",
                "Address",
                "JobTitle",
                "Title",
                "PreferredLanguage",
                "Event Type",
                "Page Url",
                "Page View Date",
                "Duration",
                "UserAgent",
                "IpInfo"};
           
            stringBuilder.AppendLine(string.Join(";", fieldColumnsList));
            var goalDefinitionManager = ServiceLocator.ServiceProvider.GetDefinitionManagerFactory().GetDefinitionManager<Sitecore.Marketing.Definitions.Goals.IGoalDefinition>();
            foreach (var contact in contacts)
            {
                try
                {
                    var contecatbehavior = contact.Entity.ContactBehaviorProfile();
                    if (contact.Entity != null)
                    {

                        string ContactEmail = "";
                        string ContactPhone = "";
                        string Address = "";
                        string Websitename = "";
                        if (contact != null)
                        {
                            EmailAddressList emailsFacetData = contact.Entity.GetFacet<EmailAddressList>();
                            if (emailsFacetData != null)
                            {
                                EmailAddress preferred = emailsFacetData.PreferredEmail;
                                ContactEmail = emailsFacetData.PreferredEmail.SmtpAddress;
                            }
                            var phoneNumber = contact.Entity.GetFacet<PhoneNumberList>();
                            if (phoneNumber != null)
                            {
                                PhoneNumber pn = phoneNumber.PreferredPhoneNumber;
                                ContactPhone = pn.Extension + " " + pn.Number;
                            }
                            var addresslist = contact.Entity.GetFacet<AddressList>();
                            if (addresslist != null)
                            {
                                Address add = addresslist.PreferredAddress;

                                Address = add.AddressLine1;
                                Address += !string.IsNullOrEmpty(Address) ? ", " + add.AddressLine2 : "";
                                Address += !string.IsNullOrEmpty(Address) ? ", " + add.AddressLine3 : "";
                                Address += !string.IsNullOrEmpty(Address) ? ", " + add.AddressLine4 : "";
                                Address += !string.IsNullOrEmpty(Address) ? ", " + add.City : "";
                                Address += !string.IsNullOrEmpty(Address) ? ", " + add.CountryCode : "";
                                Address += !string.IsNullOrEmpty(Address) ? "," + add.StateOrProvince : "";
                                Address += !string.IsNullOrEmpty(Address) ? ", " + add.PostalCode : "";
                            }
                        }



                        var webview = contact.Entity.Interactions;
                        foreach (var interaction in webview)
                        {
                            var ipinfo = interaction.IpInfo();


                            var intWebvisit = interaction.WebVisit();
                            if (intWebvisit != null)
                                Websitename = intWebvisit.SiteName;
                            if (interaction != null && interaction.Events != null && interaction.Events.Count() > 0)
                            {
                                foreach (var pevent in interaction.Events)
                                {

                                    string[] strArray = new string[18];
                                    var persion = contact.Entity.Personal();
                                    if (persion != null)
                                    {
                                        strArray[0] = !string.IsNullOrEmpty(Websitename) ? Websitename : "";
                                        strArray[1] = contact.Entity.Personal() != null && !string.IsNullOrEmpty(persion.FirstName) ? persion.FirstName : "";
                                        strArray[2] = contact.Entity.Personal() != null && !string.IsNullOrEmpty(persion.MiddleName) ? persion.MiddleName : "";
                                        strArray[3] = contact.Entity.Personal() != null && !string.IsNullOrEmpty(persion.LastName) ? persion.LastName : "";
                                        strArray[4] = contact.Entity.Personal() != null && !string.IsNullOrEmpty(persion.Nickname) ? persion.Nickname : "";
                                        strArray[5] = contact.Entity.Personal() != null && !string.IsNullOrEmpty(persion.Gender) ? persion.Gender : "";
                                        strArray[6] = !string.IsNullOrEmpty(ContactEmail) ? ContactEmail : "";
                                        strArray[7] = !string.IsNullOrEmpty(ContactPhone) ? ContactPhone : "";
                                        strArray[8] = !string.IsNullOrEmpty(Address) ? Address : "";
                                        strArray[9] = contact.Entity.Personal() != null && !string.IsNullOrEmpty(persion.JobTitle) ? persion.JobTitle : "";
                                        strArray[10] = contact.Entity.Personal() != null && !string.IsNullOrEmpty(persion.Title) ? persion.Title : "";
                                        strArray[11] = contact.Entity.Personal() != null && !string.IsNullOrEmpty(persion.PreferredLanguage) ? persion.PreferredLanguage : "";
                                    }
                                    else
                                    {
                                        strArray[0] = "Unknown";
                                        strArray[1] = "Anonymous";
                                        strArray[2] = "";
                                        strArray[3] = "";
                                        strArray[4] = "";
                                        strArray[5] = "";
                                        strArray[6] = "Unknown";
                                        strArray[7] = "";
                                        strArray[8] = "";
                                        strArray[9] = "";
                                        strArray[10] = "";
                                        strArray[11] = "";
                                    }

                                    if (pevent.GetType().Name == "PageViewEvent")
                                    {
                                        var pageview = (PageViewEvent)pevent;
                                        if (!string.IsNullOrEmpty(pageview.Url) && !pageview.Url.Contains("AddactExportData"))
                                        {
                                            strArray[12] = "Page View";
                                            strArray[13] = pageview.Url;
                                            strArray[14] = pageview.Timestamp.ToShortDateString();
                                            strArray[15] = pageview.Duration.ToString();
                                            strArray[16] = interaction.UserAgent.Replace(";", "|");
                                            strArray[17] = ipinfo != null && !string.IsNullOrEmpty(ipinfo.IpAddress) ? ipinfo.IpAddress : "";
                                        }


                                    }
                                    if (pevent.GetType().Name == "Goal")
                                    {
                                        var pagegoal = (Sitecore.XConnect.Goal)pevent;
                                        Guid goalId = pagegoal.DefinitionId;
                                        Sitecore.Marketing.Definitions.Goals.IGoalDefinition goalOne = goalDefinitionManager.Get(goalId, new CultureInfo("en"), true);
                                        strArray[12] = "Goal";
                                        strArray[13] = goalOne != null ? goalOne.Name : "";
                                        strArray[14] = pagegoal.Timestamp.ToShortDateString();
                                        strArray[15] = pagegoal.Duration.ToString();
                                        strArray[16] = interaction.UserAgent.Replace(";", "|");
                                        strArray[17] = ipinfo != null && !string.IsNullOrEmpty(ipinfo.IpAddress) ? ipinfo.IpAddress : "";
                                    }
                                    if (!string.IsNullOrEmpty(strArray[12]))
                                        stringBuilder.AppendLine(string.Join(";", strArray));
                                }
                            }

                        }

                    }
                }
                catch(Exception ex)
                {
                    Log.Error("ERROR IN EXPORT PROFILE CONTACT:", ex.Message);
                }

            }


            byte[] buffer = Encoding.ASCII.GetBytes(stringBuilder.ToString());

            return buffer;
        }
        


    }
}