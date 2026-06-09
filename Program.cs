using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TinyHttp
{
    class Program
    {
        public struct ParsedRequest
        {
            public string Method { get; init; }
            public string Path { get; init; }
            public string HttpVersion { get; init; }
        }
        public struct ParseResult
        {
            public bool Success { get; init; }
            public string ErrorMessage { get; init; }
            public ParsedRequest Request { get; init; }
        }

        public struct ValidationResult
        {
            public bool Success { get; init; }
            public string HttpStatus { get; init; }
            public string ErrorMessage { get; init; }
        }

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

            /* PARSE REQUEST */
            requestString = requestString.Replace("\r\n", "\n"); // normalize line endings
            (string status, string body) response = ("404 Not Found", "Not Found");
            var isValid = true; /* UNNECESSARY VARAIABLE */

            // Get the first line (request line)
            string[] lines = requestString.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                response = Error("400 Bad Request", "Empty request");
                isValid = false;
            }

            /* VALIDATE REQUEST */
            if (isValid)
            {
                var parseResult = ParseRequestLine(lines);

                
                if (parseResult.Success == false)
                {
                    response = Error("400 Bad Request", parseResult.ErrorMessage);
                }
                else
                {
                    // extract method, path, and HTTP version from the parsed request
                    string method = parseResult.Request.Method;
                    string path = parseResult.Request.Path;
                    string httpVersion = parseResult.Request.HttpVersion;

                    // validate the request components and get the appropriate response status and body
                    ValidationResult validationResult = ValidateRequest(method, path, httpVersion);     

                    /* ROUTE REQUEST */
                    if (validationResult.Success == true) // validation succeeded, get the appropriate response body for the path
                    {
                        /* GET RESPONSE BODY */
                        response = GetResponseBody(path);
                    }
                    else    // validation failed, return the appropriate error response
                    {
                        response = (validationResult.HttpStatus, validationResult.ErrorMessage);
                    }
                }
            }
            
            /* SEND RESPONSE */ 
            string httpResponse =   BuildHTTPResponse(response);
            
            byte[] responseBytes = Encoding.UTF8.GetBytes(httpResponse);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
 
            // The connection closes automatically when 'client' is disposed
        }

        static (string status, string body) GetResponseBody(string path) // helper function to determine the response body based on the request path 
        {
            return path switch
            {
                "/" => ("200 OK", "Home"),
                "/about" => ("200 OK", "About"),
                "/hello" => ("200 OK", "Hello, World!"),
                _ => ("404 Not Found", " Page not found")
            };
        }

        static (string status, string body) Error(string status, string message)  // helper function to create error responses 
        {
            return (status, message);
        }

        /* HTTP response builder */
        static string BuildHTTPResponse((string status, string body) response)  // builds a complete HTTP response string from the status and body
        {
            return $"HTTP/1.1 {response.status}\r\n" +
                    "Content-Type: text/plain\r\n" +
                    "Connection: close\r\n\r\n" + 
                    $"{response.body}";   
        }

        public static ParseResult ParseRequestLine(string[] lines)  // Parses the request line into structured data
        {
            string requestLine = lines[0]; // "GET / HTTP/1.1"
            string[] parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3) 
            {
                return new ParseResult
                {
                    Success = false,
                    ErrorMessage = "Malformed request",
                    Request = default
                };
            }

            return new ParseResult      // create and return ParseResult immediately - prevents mutating
            {
                Success = true,
                ErrorMessage = string.Empty,
                Request = new ParsedRequest
                {
                    Method = parts[0],
                    Path = parts[1],
                    HttpVersion = parts[2]
                }
            };
        }

        public static ValidationResult ValidateRequest(string method, string path, string version) // validates the request components and returns a ValidationResult indicating success or failure along with appropriate HTTP status and error message 
        {
            if (method == "GET") // only support GET 
            {
                if (path.StartsWith('/')) // only support paths starting with "/"
                {
                    if (version != "HTTP/1.1") // only support HTTP/1.1
                    {
                        Console.WriteLine("[WARN] HTTP version not supported");
                        return new ValidationResult
                        {
                            Success = false,
                            HttpStatus = "505 HTTP Version Not Supported",
                            ErrorMessage = "Only HTTP/1.1 is supported"
                        };
                    }
                    else
                    {
                        Console.WriteLine($"[INFO] GET {path}");
                        return new ValidationResult
                        {
                            Success = true,
                            HttpStatus = string.Empty,
                            ErrorMessage = string.Empty
                        };
                    }
                }
                else
                {
                    Console.WriteLine("[WARN] Invalid path");
                    return new ValidationResult
                    {
                        Success = false,
                        HttpStatus = "400 Bad Request",
                        ErrorMessage = "Invalid path"
                    };
                }
            }
            else
            {
                Console.WriteLine("[WARN] Method not allowed");
                return new ValidationResult
                {
                    Success = false,
                    HttpStatus = "405 Method Not Allowed",
                    ErrorMessage = "Method not supported"                  
                };
            }
        }
    }
}