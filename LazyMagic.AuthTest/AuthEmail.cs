namespace LazyMagic.Client.Auth;

/// <summary>
/// Opens your email account and retrieves authorization codes sent to that 
/// email account by the AuthProvider.
/// </summary>
public static class AuthEmail
{
    public static string GetAuthCode(IConfiguration appConfig, string emailTo)
    {
        // Start SignUp process - will send  a verification code to specified email account
        var domain = appConfig["EmailAccount:Domain"];

        int port = 993;
        Int32.TryParse(appConfig["EmailAccount:Port"], out port);
        bool useSSL = true;
        bool.TryParse(appConfig["EmailAccount:UseSSL"], out useSSL);
        var email = appConfig["EmailAccount:Email"];
        var emailPassword = appConfig["EmailAccount:Password"];
        var verificationCode = string.Empty;

        bool foundCode = false;
        var tryCount = 0;
        do
        {
            Debug.WriteLine($"tryCount {tryCount}");
            try
            {
                tryCount++;
                var messages = new List<string>();
                using var mailClient = new ImapClient();
                //mailClient.Connect(domain, port, useSSL);
                // Note: since we don't have an OAuth2 token, disable
                // the XOAUTH2 authentication mechanism.
                mailClient.AuthenticationMechanisms.Remove("XOAUTH2");

                mailClient.Authenticate(email, emailPassword);

                var inbox = mailClient.Inbox;

                var query = SearchQuery.ToContains(emailTo);

                inbox.Open(FolderAccess.ReadOnly);

                var results = inbox.Search(query);

                if (results.Count > 0)
                {
                    // Grab the latest entry
                    var message = inbox.GetMessage(results[results.Count - 1]);
                    var bodyparts = message.HtmlBody.Split(" ");
                    verificationCode = bodyparts[^1];
                    foundCode = true;
                }
                mailClient.Disconnect(true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Can't connect to email server {e.Message}");
            }
            if (tryCount > 5)
                throw new Exception("Failed to find email with auth code");

            if (!foundCode)
                Thread.Sleep(1000);

        } while (!foundCode);

        return verificationCode;
    }
}
