using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Udacity.AzureAiEngineer.EnrichingData
{
    
    public static class AnalyzeColor
    {
        /* 

          This is where you will configure your Spring API Key
          
        */
        static readonly string cogServicesEndpoint = "https://mattscognitiveservice.cognitiveservices.azure.com/";
        static readonly string cognitiveServicesKey = "b24774d2e84b49f3b6546d46cefc62c3";

        #region Class used to deserialize the request
        private class InputRecord
        {
            public class InputRecordData
            {
                public string thumbnail { get; set; }
            }

            public string RecordId { get; set; }
            public InputRecordData Data { get; set; }
        }

        private class WebApiRequest
        {
            public List<InputRecord> Values { get; set; }
        }
        #endregion

        #region Classes used to serialize the response

        private class OutputRecord
        {
            public class OutputRecordData
            {
                public string Foreground { get; set; } = "";
                public string Background { get; set; } = "";
            }

            public class OutputRecordMessage
            {
                public string Message { get; set; }
            }

            public string RecordId { get; set; }
            public OutputRecordData Data { get; set; }
            public List<OutputRecordMessage> Errors { get; set; }
            public List<OutputRecordMessage> Warnings { get; set; }
        }

        private class WebApiResponse
        {
            public List<OutputRecord> Values { get; set; }
        }
        #endregion
        [FunctionName("AnalyzeColor")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
           log.LogInformation("Entity Search function: C# HTTP trigger function processed a request.");

            var response = new WebApiResponse
            {
                Values = new List<OutputRecord>()
            };

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            var data = JsonConvert.DeserializeObject<WebApiRequest>(requestBody);

            // Do some schema validation
            if (data == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema.");
            }
            if (data.Values == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema. Could not find values array.");
            }

            // Calculate the response for each value.
            foreach (var record in data.Values)
            {
                if (record == null || record.RecordId == null) continue;

                OutputRecord responseRecord = new OutputRecord
                {
                    RecordId = record.RecordId
                };

                try
                {
                    responseRecord.Data = GetColors(record).Result;
                }
                catch (Exception e)
                {
                    // Something bad happened, log the issue.
                    var error = new OutputRecord.OutputRecordMessage
                    {
                        Message = e.Message
                    };

                    responseRecord.Errors = new List<OutputRecord.OutputRecordMessage>
                    {
                        error
                    };
                }
                finally
                {
                    response.Values.Add(responseRecord);
                }
            }

            return (ActionResult)new OkObjectResult(response);
        }

        private async static Task<OutputRecord.OutputRecordData> GetColors(InputRecord input)
        {
            var uri = cogServicesEndpoint + "/vision/v3.2/analyze?visualFeatures=Color";
            var result = new OutputRecord.OutputRecordData() {Background="unknown", Foreground="unknown"};
            var payload = "{\"url\":\"" + input.Data.thumbnail + "\"}";


            using (var client = new HttpClient()){
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", cognitiveServicesKey);
                
                var data = new StringContent(payload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(uri, data);
                string responseBody = await response?.Content?.ReadAsStringAsync();
                
                try{
                    JObject serviceResults = JObject.Parse(responseBody);
                    JToken token = serviceResults["color"];
                    
                    result.Background = token.Value<string>("dominantColorBackground");
                    result.Foreground = token.Value<string>("dominantColorForeground");
                }catch(Exception){
                    //just use "unknown"
                }
            }

            return result;
        }
    }
}
