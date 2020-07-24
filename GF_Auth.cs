using System;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;

namespace GF_Auth
{
    public class Nostale
    {
        private readonly HttpClient http = new HttpClient();

        private readonly string game_id = "dd4e22d6-00d1-44b9-8126-d8b40e0cd7c9"; // Nostale GameID
        private readonly string url_login = "https://gameforge.com/api/v1/auth/sessions";
        private readonly string url_accounts = "https://gameforge.com/api/v1/user/accounts";
        private readonly string url_token = "https://spark.gameforge.com/api/v1/auth/thin/codes";
        private string tnt_installid = "fc7b3123-3c68-425f-712f-01b5b1315ae2"; // This will gets generated on login call
        private string auth_token;
        private JObject accounts;
        public List<string> Usernames { get; set; } = new List<string>();

        string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        void build_tntid(string user, string pass, string locale)
        {
            tnt_installid = CreateMD5(user) + CreateMD5(locale) + CreateMD5(pass);
        }

        string convertToken(string binvalue)
        {
            //a -> 97 -> 0x61 -> 61
            //8 -> 56 -> 0x38 -> 38
            // Input: a857263a-3fc1-4c60-ad78-9b6d9a2a0691
            // Output: 61383537323633612D336663312D346336302D616437382D396236643961326130363931
            string res = "";
            for (int i = 0; i < binvalue.Length; i++)
            {
                res += (((Int32)binvalue[i]).ToString("X"));
            }
            return res;
        }

        public async Task Login(string mail, string pass, string locale)
        {
            build_tntid(mail, pass, locale);
            var values = new Dictionary<string, string>
            {
                { "email",mail} ,{"locale",locale}, {"password",pass}
            };

            var requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url_login),
                Headers = {
                    {"User-Agent","Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/72.0.3626.121 Safari/537.36"},
                    {"tnt-installation-id", tnt_installid},
                    {"Origin", "spark://www.gameforge.com"},
                  },
                Content = new System.Net.Http.StringContent(JsonConvert.SerializeObject(values), Encoding.UTF8, "application/json")
            };
            var res = await http.SendAsync(requestMessage);
            if ((int)res.StatusCode != 201)
                throw new Exception("LoginFail");

            var stringres = await res.Content.ReadAsStringAsync();
            JObject json = JObject.Parse(stringres);

            if (json["token"] == null)
                throw new Exception("LoginTokenFail");

            auth_token = (string)json["token"];
        }

        public async Task<List<string>> GetAccounts()
        {
            var requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url_accounts),
                Headers = {
                    {HttpRequestHeader.Authorization.ToString(), $"Bearer {auth_token}"},
                    {"tnt-installation-id", tnt_installid},
                    {"Origin", "spark://www.gameforge.com"},
                    {"Connection","Keep-Alive"},
                    {"user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.97 Safari/537.36"}
                }
            };
            var res = await http.SendAsync(requestMessage);
            if ((int)res.StatusCode != 200)
                throw new Exception("GetAccountDataFail");

            var stringres = await res.Content.ReadAsStringAsync();

            accounts = JObject.Parse(stringres);

            Usernames.Clear();
            Console.WriteLine($"Found {accounts.Count} accounts.");
            foreach (JProperty p in accounts.Properties())
            {
                if ((string)accounts[p.Name]["gameId"] == game_id)
                {
                    Console.WriteLine($"Account: {accounts[p.Name]["displayName"]}");
                    Usernames.Add((string)accounts[p.Name]["displayName"]);
                }
            }

            return Usernames;
        }

        public async Task<string> GetToken(string username)
        {
            string account = null;
            foreach (JProperty p in accounts.Properties())
            {
                if ((string)accounts[p.Name]["gameId"] == game_id)
                {
                    if (username == (string)accounts[p.Name]["displayName"])
                    {
                        account = (string)p.Name;
                    }
                }
            }

            if (account == null)
                throw new Exception("User not found in account.");

            var values = new Dictionary<string, string>
            {
                {"platformGameAccountId", account}
            };

            Console.WriteLine($"Using account {account}({username}) to get token.");

            var requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url_token),
                Headers = {
                    {"User-Agent","GameforgeClient/2.1.10"},
                    {"Authorization", $"Bearer {auth_token}"},
                    {"tnt-installation-id", tnt_installid},
                    {"Origin", "spark://www.gameforge.com"},
                    {"Connection","Keep-Alive"},
                },
                Content = new System.Net.Http.StringContent(JsonConvert.SerializeObject(values), Encoding.UTF8, "application/json")
            };
            var res = await http.SendAsync(requestMessage);
            var stringres = await res.Content.ReadAsStringAsync();
            Console.WriteLine(stringres);
            if ((int)res.StatusCode != 201)
                throw new Exception($"GetUserTokenFail-{(int)res.StatusCode}");

            JObject json = JObject.Parse(stringres);

            if (json["code"] == null)
                throw new Exception("UserTokenNull");

            string token = convertToken((string)json["code"]);

            return token;
        }

        public void Example()
        {
            try
            {
                Login("example@gmail.com", "passwordhere", "de_DE").Wait(); // Don't forget to set correct locale
                GetAccounts().Wait();
                GetToken("AccountNameHere").Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
