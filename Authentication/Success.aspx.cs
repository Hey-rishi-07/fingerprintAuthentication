using System;
using System.Web.UI;

namespace Authentication
{
    public partial class Success : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                string pcno = Session["AuthenticatedPCNO"] as string;
                if (string.IsNullOrEmpty(pcno))
                {
                    // Fallback for testing if navigated directly
                    pcno = "1001";
                }

                litPcNo.Text = pcno;
                litMatchScore.Text = (Session["MfaScore"] ?? 3850).ToString();
                litMode.Text = (Session["MfaMode"] ?? "Demo Mode").ToString();

                object timeObj = Session["MfaTime"];
                DateTime dt = (timeObj is DateTime) ? (DateTime)timeObj : DateTime.Now;
                litTimestamp.Text = dt.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            Session["AuthenticatedPCNO"] = null;
            Session["PendingPCNO"] = null;
            Session["MfaScore"] = null;
            Session["MfaTime"] = null;
            Response.Redirect("Login.aspx");
        }
    }
}
