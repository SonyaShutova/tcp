using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace ConsoleServer
{
    class Client
    {
        private void Response(TcpClient Client, string Text, int Code)
        {
            string Html = "<html><body><h3>" + Text + "</h3></body></html>";
            int HtmlLen = Encoding.UTF8.GetBytes(Html).Length;
            string HeadersStr = "HTTP/1.1 " + Code + "\nContent-type: text/html; charset=utf-8\nContent-Length:" + /*Html.Length.ToString()*/HtmlLen + "\n\n" + Html;
            byte[] Buffer = Encoding.UTF8.GetBytes(HeadersStr);
            Client.GetStream().Write(Buffer, 0, Buffer.Length);
            Client.Close();
        }
        private void SendError(TcpClient Client, int Code)
        {
            string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            string Html = "<html><body><h1>" + CodeStr + "</h1></body></html>";
            Response(Client, Html, Code);
        }
        public Client(TcpClient Client)
        {
            string Request = "";
            byte[] Buffer = new byte[1024];
            int Count;
            while ((Count = Client.GetStream().Read(Buffer, 0, Buffer.Length)) > 0)
            {
                Request += Encoding.ASCII.GetString(Buffer, 0, Count);
                if (Request.IndexOf("\r\n\r\n") >= 0 || Request.Length > 4096)
                {
                    break;
                }
            }
            Match ReqMatch = Regex.Match(Request, @"^\w+\s+([^\s\?]+)[^\s]*\s+HTTP/.*|");
            if (ReqMatch == Match.Empty)
            {
                SendError(Client, 400);
                return;
            }

            string RequestUri = ReqMatch.Groups[1].Value;
            RequestUri = Uri.UnescapeDataString(RequestUri);
            if (RequestUri.IndexOf("..") >= 0)
            {
                SendError(Client, 400);
                return;
            }
            Console.WriteLine(RequestUri); 

            if (RequestUri == "/user")
            {
                string url = "http://localhost" + Regex.Match(Request, @"GET\s(.*?)\sHTTP").Groups[1].Value;
                var queryString = new Uri(url).Query;
                var parameters = HttpUtility.ParseQueryString(queryString);

                string paramName = parameters["Name"] ?? "";
                string paramEmail = parameters["Email"] ?? "";
                string paramAge = parameters["Age"] ?? "";

                string responseText = "";
                if (paramName.Length <= 1) 
                {
                    responseText += "Имя должно быть больше 1 символа" + "<br>";
                }

                if (! new Regex(@"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$").IsMatch(paramEmail))
                {
                    responseText += "Email некорректный" + "<br>";
                }

                try
                {
                    int Age = Convert.ToInt16(paramAge);
                    if (Age < 1 || Age > 120)
                    {
                        responseText += "Возраст должен быть от 1 до 120" + "<br>";
                    }
                }
                catch (FormatException)
                {
                    responseText += "Некорректный формат числа" + "<br>";
                }

                if (responseText.Length > 0)
                {
                    Response(Client, responseText, 400);
                }
                else 
                {
                    responseText += "Ваше имя: " + paramName + "<br>" +
                        "Ваш email: " + paramEmail + "<br>" +
                        "Ваш возраст: " + paramAge + "<br>";
                    Response(Client, responseText, 200);
                }
                return;
            }
            if (RequestUri.EndsWith("/"))
            {
                RequestUri += "index.html";
            }
            string FilePath = "../../../www/" + RequestUri;
            if (!File.Exists(FilePath))
            {
                SendError(Client, 404);
                return;
            }
            string Extension = RequestUri.Substring(RequestUri.LastIndexOf('.'));
            string ContentType = "";
            switch (Extension)
            {
                case ".htm":
                case ".html":
                    ContentType = "text/html";
                    break;
                case ".css":
                    ContentType = "text/stylesheet";
                    break;
                case ".js":
                    ContentType = "text/javascript";
                    break;
                case ".jpg":
                    ContentType = "image/jpeg";
                    break;
                case ".jpeg":
                case ".png":
                case ".gif":
                    ContentType = "image/" + Extension.Substring(1);
                    break;
                default:
                    if (Extension.Length > 1)
                    {
                        ContentType = "application/" + Extension.Substring(1);
                    }
                    else
                    {
                        ContentType = "application/unknown";
                    }
                    break;
            }
            FileStream FS;
            try
            {
                FS = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception)
            {
                SendError(Client, 500);
                return;
            }
            string Headers = "HTTP/1.1 200 OK\nContent-Type: " + ContentType + "\nContent-Length: " + FS.Length + "\n\n";
            byte[] HeadersBuffer = Encoding.ASCII.GetBytes(Headers);
            Client.GetStream().Write(HeadersBuffer, 0, HeadersBuffer.Length);
            while (FS.Position < FS.Length)
            {
                Count = FS.Read(Buffer, 0, Buffer.Length);
                Client.GetStream().Write(Buffer, 0, Count);
            }

            FS.Close();
            Client.Close();
        }
    }
}
