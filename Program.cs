using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
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

            public List<Header> Headers { get; init; }
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
        public struct Header
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }
        public struct HttpResponse
        {
            public string Status { get; init; }
            public string Body { get; init; }
            List<Header> Headers { get; init; }
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

            // process request
            HttpResponse response = ProcessHttpRequest(requestString);
            
            /* SEND RESPONSE */ 
            string httpResponse = BuildHTTPResponse(response);
            
            byte[] responseBytes = Encoding.UTF8.GetBytes(httpResponse);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
 
            // The connection closes automatically when 'client' is disposed
        }

        static HttpResponse GetResponseBody(string path) // helper function to determine the response body based on the request path 
        {
            return path switch
            {
                "/" => new HttpResponse { Status = "200 OK", Body = "Home" },
                "/about" => new HttpResponse { Status = "200 OK", Body = "About" },
                "/hello" => new HttpResponse { Status = "200 OK", Body = "Hello, World!" },
                _ => new HttpResponse { Status = "404 Not Found", Body = "Page not found" }
            };
        }

        static HttpResponse Error(string status, string message)  // helper function to create error responses 
        {
            return new HttpResponse
            {
                Status = status,
                Body = message
            };
        }

        static string BuildHTTPResponse(HttpResponse response)  // builds a complete HTTP response string from the status and body
        {
            return $"HTTP/1.1 {response.Status}\r\n" +
                    "Content-Type: text/plain\r\n" +
                    "Connection: close\r\n\r\n" + 
                    $"{response.Body}";   
        }

        public static ParseResult ParseRequest(string[] lines)  // Parses the request line into structured data
        {
            // Parse request line
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

            // Parse headers
            List<Header> headerList = [];

            for (int i = 1; i < lines.Length; i++)
            {
                string[] headerParts = lines[i].Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
                
                if (headerParts.Length != 2)
                {
                    return new ParseResult
                    {
                        Success = false,
                        ErrorMessage = "Malformed header",
                        Request = default
                    };
                }

                if (string.IsNullOrWhiteSpace(headerParts[0].Trim()))   // return error msg if Header Name is empty
                {
                    return new ParseResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid header name",
                        Request = default
                    };
                }
                else
                {
                    // add header
                    headerList.Add(new Header
                    {
                        Name = headerParts[0].Trim(),
                        Value = headerParts[1].Trim()
                    });
                }           
            }

            return new ParseResult      // create and return ParseResult immediately - prevents mutating
            {
                Success = true,
                ErrorMessage = string.Empty,
                Request = new ParsedRequest
                {
                    Method = parts[0],
                    Path = parts[1],
                    HttpVersion = parts[2],
                    Headers = headerList
                }
            };
        }

        public static Header? GetHeader(List<Header> headers, string name)
        {
            if (headers == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (var header in headers)
            {
                if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return header;
                }
            }

            return null;
        }

        public static ValidationResult ValidateMethod(String method)
        {
            if (method != "GET")
            {
                Console.WriteLine("[WARN] Method not allowed");
                return new ValidationResult
                {
                    Success = false,
                    HttpStatus = "405 Method Not Allowed",
                    ErrorMessage = "Method not supported"                  
                };
            }
            else
            {
                return new ValidationResult
                {
                    Success = true,
                    HttpStatus = string.Empty,
                    ErrorMessage = string.Empty
                };
            }
        }

        public static ValidationResult ValidatePath(String path)
        {
            if (!path.StartsWith('/')) // only support paths starting with "/"
            {
                Console.WriteLine("[WARN] Invalid path");
                return new ValidationResult
                {
                    Success = false,
                    HttpStatus = "400 Bad Request",
                    ErrorMessage = "Invalid path"
                };
            }
            else
            {
                return new ValidationResult
                {
                    Success = true,
                    HttpStatus = string.Empty,
                    ErrorMessage = string.Empty
                };
            }
        }

        public static ValidationResult ValidateVersion(String version)
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
                return new ValidationResult
                {
                    Success = true,
                    HttpStatus = string.Empty,
                    ErrorMessage = string.Empty
                };
            }
        }

        public static ValidationResult ValidateRequest(string method, string path, string version) // validates the request components and returns a ValidationResult indicating success or failure along with appropriate HTTP status and error message 
        {

            ValidationResult result = ValidateMethod(method);
            
            if (!result.Success)
                return result;

            result = ValidatePath(path);

            if (!result.Success)
                return result;

            result = ValidateVersion(version);

            if (!result.Success)
                return result;

            return result;
        }

        public static HttpResponse ProcessHttpRequest(String requestString)
        {
            /* PARSE REQUEST */
            requestString = requestString.Replace("\r\n", "\n"); // normalize line endings
            HttpResponse response = new HttpResponse()
            {
                Status = "404 Not Found",
                Body = "Not Found"
            };

            // Get the first line (request line)
            string[] lines = requestString.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                response = Error("400 Bad Request", "Empty request");
            }

            /* VALIDATE REQUEST */
            var parseResult = ParseRequest(lines);
            
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
                List<string> headers = parseResult.Request.Headers.ConvertAll(h => $"{h.Name}: {h.Value}");

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
                    response = new HttpResponse
                    {
                        Status = validationResult.HttpStatus,
                        Body = validationResult.ErrorMessage
                    };
                }

                Console.WriteLine("Parsed headers: ");
                foreach (var header in headers)
                {
                    Console.WriteLine(header);
                }
            }
            return response;
        }
    }
}

