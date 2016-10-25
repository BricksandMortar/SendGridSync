using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using com.bricksandmortarstudio.SendGridSync.Constants;
using com.bricksandmortarstudio.SendGridSync.DTO;
using Newtonsoft.Json;
using RestSharp;
using Rock.Model;

namespace com.bricksandmortarstudio.SendGridSync.Helper
{
    public class ApiHelper
    {
        internal static bool CreateCustomField( string name, string type, string apiKey )
        {
            var restClient = new RestClient( SendGridRequest.SENDGRID_BASE_URL );
            var request = new RestRequest( Method.POST )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.CUSTOM_FIELDS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );

            request.AddBody( new { name = name, type = type } );
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

        internal static IRestRequest BuildContactsRequest( IEnumerable<PersonAlias> people, string apiKey )
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

        private static IRestResponse CreateListRequest( string apiKey, Method method, string body = "" )
        {
            var restClient = new RestClient( SendGridRequest.SENDGRID_BASE_URL );
            var request = new RestRequest( method )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.LISTS_RESOURCE
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

        internal static void AddPeopleToList(IQueryable<string> emailAddresses, int listId, string apiKey)
        {
            var ids = emailAddresses.Select(GetRecipientId).ToList();
            string body = JsonConvert.SerializeObject( ids,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                } );

            var response = CreateListRequest( apiKey, Method.GET, body );
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception( "Unable to add people to list" );
            }
        }

        private static string GetRecipientId( string email )
        {
            return Convert.ToBase64String( Encoding.ASCII.GetBytes( email ) );
        }
    }
}