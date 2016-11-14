using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using com.bricksandmortarstudio.SendGridSync.Constants;
using com.bricksandmortarstudio.SendGridSync.DTO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Rock.Model;

namespace com.bricksandmortarstudio.SendGridSync.Helper
{
    public class ApiHelper
    {
        static readonly char[] Padding = { '=' };

        internal static bool CreateCustomField( string name, string type, string apiKey )
        {
            var restClient = new RestClient( SendGridRequest.SENDGRID_BASE_URL );
            var request = new RestRequest( Method.POST )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.CUSTOM_FIELDS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );

            request.AddBody( new {name, type } );
            var response = restClient.Execute( request );
            if ( response.StatusCode == HttpStatusCode.Created )
            {
                return true;
            }
            throw new Exception( string.Format( "Unable to create SendGrid custom field {0}", name ) );
        }

        public static CustomFields GetCustomFields( string apiKey )
        {
            var restClient = new RestClient( SendGridRequest.SENDGRID_BASE_URL );
            var request = new RestRequest( Method.GET )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.CUSTOM_FIELDS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );
            var response = restClient.Execute( request );
            if ( response.StatusCode == HttpStatusCode.OK )
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                return JsonConvert.DeserializeObject<CustomFields>( response.Content, settings );
            }
            throw new Exception( "Unable to obtain existing custom fields from SendGrid" );
        }

        internal static IRestRequest AddContactsRequestBuilder( IEnumerable<PersonAlias> people, string apiKey )
        {
            var payload = people.Select( p => new SendGridPerson( p.Person.Email, p.Id, p.Person.FirstName, p.Person.LastName, p.Person.TitleValue != null ? p.Person.TitleValue.Value : "" ) ).ToList();
            var request = new RestRequest( Method.POST )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.RECIPIENTS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );
            request.AddParameter( "application/json", JsonConvert.SerializeObject( payload,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                } ), ParameterType.RequestBody );
            return request;
        }

        internal static IRestResponse NewRemoveContactsRequest(IEnumerable<string> emails, string apiKey)
        {
            var restClient = new RestClient( SendGridRequest.SENDGRID_BASE_URL );
            var recipientIds = GetRecipientIds(emails);
            var request = new RestRequest( Method.DELETE )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.RECIPIENTS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );
            request.AddParameter( "application/json", JsonConvert.SerializeObject( recipientIds,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                } ), ParameterType.RequestBody );
            return restClient.Execute(request);
        }

        internal static int? DoesListExist( string listName, string apiKey )
        {
            var response = CreateListRequest( apiKey, Method.GET );
            if ( response.StatusCode == HttpStatusCode.OK )
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                var lists = JsonConvert.DeserializeObject<ListsResponse>( response.Content, settings );
                var existingList = lists.lists.FirstOrDefault( l => String.Equals( l.name, listName, StringComparison.OrdinalIgnoreCase ) );
                return existingList?.id;
            }
            throw new Exception( "Unable to obtain existing existing lists from SendGrid" );
        }

        internal static int GetListRecipientCount( int listId, string apiKey )
        {
            var response = CreateListRequest( apiKey, Method.GET, string.Empty, listId.ToString() );
            if ( response.StatusCode == HttpStatusCode.OK )
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                var list = JsonConvert.DeserializeObject<List>( response.Content, settings );
                return list.recipient_count;
            }
            throw new Exception( "Unable to obtain existing existing lists from SendGrid" );
        }

        private static IRestResponse CreateListRequest( string apiKey, Method method, string body = "", string additionalResource = "" )
        {
            var restClient = new RestClient( SendGridRequest.SENDGRID_BASE_URL );
            var request = new RestRequest( method )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.LISTS_RESOURCE + ( !string.IsNullOrWhiteSpace(additionalResource) ? "/" + additionalResource : "")
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );
            if ( !string.IsNullOrWhiteSpace( body ) )
            {
                request.AddParameter( "application/json", body, ParameterType.RequestBody );
            }
            var response = restClient.Execute( request );
            return response;
        }

        internal static int? CreateNewList( string apiKey, string listName )
        {
            string body = JsonConvert.SerializeObject( new List { name = listName },
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                } );

            var response = CreateListRequest( apiKey, Method.POST, body );
            if ( response.StatusCode == HttpStatusCode.Created )
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                var list = JsonConvert.DeserializeObject<List>( response.Content, settings );
                if ( list != null )
                {
                    return list.id;
                }
                throw new Exception( "Unable to deserialize created list" );
            }
            throw new Exception( "Unable to create new list" );
        }

        private static IRestResponse CreatePersonListRequest( string apiKey, int listId, Method method, string body = "", Dictionary<string, string> parameters = null  )
        {
            var restClient = new RestClient( SendGridRequest.SENDGRID_BASE_URL );
            var request = new RestRequest( method )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.LISTS_RESOURCE + "/" + listId + "/" + SendGridRequest.LISTS_PERSON_RESOUCE
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );
            if ( !string.IsNullOrWhiteSpace( body ) )
            {
                request.AddParameter( "application/json", body, ParameterType.RequestBody );
            }
            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    request.AddParameter(parameter.Key, parameter.Value);
                }
            }
            var response = restClient.Execute( request );
            return response;
        }

        internal static IEnumerable<int> GetListPersonAliasIds(string apiKey, int listId, int listCount)
        {
            int parsedCount = 0;
            var personAliasIds = new List<int>();
            int page = 1;
            while (listCount > 0)
            {
                var parameters = new Dictionary<string, string>
                {
                    {"page_size", "1000"},
                    {"page", page.ToString()}
                };
                var response = CreatePersonListRequest( apiKey, listId, Method.GET, null, parameters );
                if ( response.StatusCode != HttpStatusCode.OK )
                {
                    throw new Exception( "Unable to fetch list people " + response.Content );
                }
                dynamic payload = JObject.Parse(response.Content);
                dynamic listPeople = payload.recipients;
                foreach ( var person in listPeople )
                {
                    parsedCount++;
                    JArray customFields = person.custom_fields;
                    JObject personAliasField = customFields.Children<JObject>()
                              .FirstOrDefault(o => o["name"] != null && o["name"].ToString() == "person_alias_id");
                    if (personAliasField?["value"] != null && !string.IsNullOrWhiteSpace( personAliasField["value"].ToString() ) )
                    {
                        string personAlisFieldValue = personAliasField["value"].ToString();
                        int personAliasId;
                        bool succesfulParse = int.TryParse(personAlisFieldValue, out personAliasId);
                        if (succesfulParse)
                        {
                            personAliasIds.Add( personAliasId );
                        }
                    }
                }
                listCount = listCount - parsedCount;
                page++;
            }
            return personAliasIds;
        }

        internal static void DeleteRecipients(string apiKey, IEnumerable<string> recipientIds)
        {
            var response = NewRemoveContactsRequest(recipientIds, apiKey);
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new Exception("Could not delete recipients " + response.Content);
            }
        }

        internal static void AddPeopleToList(IEnumerable<string> emailAddresses, int listId, string apiKey)
        {
            var ids = emailAddresses.Select(GetRecipientId).ToList();
            string body = JsonConvert.SerializeObject( ids,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                } );
            
            var response = CreatePersonListRequest( apiKey,listId, Method.POST, body );
            if (response.StatusCode != HttpStatusCode.Created)
            {
                throw new Exception( "Unable to add people to list: " + response.Content);
            }
        }

        private static string GetRecipientId( string email )
        {
            return Convert.ToBase64String( Encoding.ASCII.GetBytes(email.ToLower()) ).TrimEnd( Padding ).Replace( '+', '-' ).Replace( '/', '_' );
        }

        private static IEnumerable<string> GetRecipientIds(IEnumerable<string> emailList)
        {
            return emailList.Select(GetRecipientId).ToList();
        }
    }
}