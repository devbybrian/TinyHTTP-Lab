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
            (string status, string body) response = ("404 Not Found", "Not Found");
            var isValid = true;

            // Get the first line (request line)
            string[] lines = requestString.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                response = Error("400 Bad Request", "Empty request");
                isValid = false;
            }

            if (isValid)
            {
                string requestLine = lines[0]; // "GET / HTTP/1.1"

                // split into method, path, and version
                string[] parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length != 3)  // malformed request line
                {
                    response = Error("400 Bad Request", "Malformed  request line");
                    Console.WriteLine("[WARN] Malformed request line");
                    isValid = false;
                }
                else
                {
                    string method = parts[0];
                    string path = parts[1];
                    string httpVersion = parts[2];

                    /* REQUEST ROUTING */
                    if (method == "GET") // only support GET 
                    {
                        if (path.StartsWith("/")) // only support paths starting with "/"
                        {
                            if (httpVersion != "HTTP/1.1") // only support HTTP/1.1
                            {
                                response = Error("505 HTTP Version Not Supported", "Only HTTP/1.1 is supported");
                                Console.WriteLine("[WARN] HTTP version not supported");
                            }
                            else
                            {
                                response = GetResponseBody(path);
                                Console.WriteLine($"[INFO] GET {path}");
                            }
                        }
                        else
                        {
                            response = Error("400 Bad Request", "Invalid path");
                            Console.WriteLine("[WARN] Invalid path");
                        }
                    }
                    else // only GET is supported in this tiny server
                    {
                        response = Error("405 Method Not Allowed", "Method not supported");
                        Console.WriteLine("[WARN] Method not allowed");
                    }

                    
                }
            }
            
            

            // Parse and route only if request is valid
            if (isValid)
            {
                
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

        static (string status, string body) Error(string status, string message)
        {
            return (status, message);
        }
    }
}