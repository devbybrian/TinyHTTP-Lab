using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TinyHttp
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            /* SERVER SETUP */ 
            // Create a TCP listener on port 8080
            TcpListener listener = new TcpListener(IPAddress.Any, 8080);
            listener.Start();   // start listening for client request
            Console.WriteLine("TinyHttp server is running on port 8080...");

            /* WAIT FOR BROWSER */
            bool running = true;
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; running = false; };
            while (running)
            {
                // wait for browser to connect
                using TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Browser connected!");      

                // handle the incoming data and send a response    
                await HandleBrowserRequest(client);      
            }

            listener.Stop();
        }

        
        private static async Task HandleBrowserRequest(TcpClient client)
        {
            using NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];

            /* READ REQUEST */ 
            // read the browser's HTTP request
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            string requestString = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"Request received:\n{requestString}");

            /* REQUEST PARSING */
            requestString = requestString.Replace("\r\n", "\n"); // normalize line endings

            // Get the first line (request line)
            string[] lines = requestString.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                Console.WriteLine("Empty request received.");
                return; 
            }

            string requestLine = lines[0]; // "GET / HTTP/1.1"

            // split into method, path, and version
            string[] parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                Console.WriteLine("Malformed request line.");
                return;
            }

            string method = parts[0];
            string path = parts[1];
            string httpVersion = parts[2];

            /* ROUTING LOGIC */
            (string status, string body) response = ("404 Not Found", "HTTP/1.1 404 Not Found");

            if (method == "GET")
            {
                response = GetResponseBody(path);
            }

            /* SEND RESPONSE */ 
            // send a standard HTTP response back
            string httpResponse =   $"HTTP/1.1 {response.status}\r\n" +
                                    "Content-Type: text/plain\r\n" +
                                    "Connection: close\r\n\r\n" + 
                                    $"{response.body}";
            
            byte[] responseBytes = Encoding.UTF8.GetBytes(httpResponse);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
 
            // The connection closes automatically when 'client' is disposed
        }

        static (string status, string body) GetResponseBody(string path)
        {
            return path switch
            {
                "/" => ("200 OK", "Home"),
                "/about" => ("200 OK", "About"),
                "/hello" => ("200 OK", "Hello, World!"),
                _ => ("404 Not Found", " Page not found")
            };
        }
    }
}