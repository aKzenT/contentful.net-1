﻿using Contentful.Core.Configuration;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Contentful.Core.Tests
{
    public class ContentfulManagementClientTests : ClientTestsBase
    {
        private ContentfulManagementClient _client;
        private FakeMessageHandler _handler;
        private HttpClient _httpClient;
        public ContentfulManagementClientTests()
        {
            _handler = new FakeMessageHandler();
            _httpClient = new HttpClient(_handler);
            _client = new ContentfulManagementClient(_httpClient, new ContentfulOptions()
            {
                DeliveryApiKey = "123",
                ManagementApiKey = "564",
                SpaceId = "666",
                UsePreviewApi = false
            });
        }

        [Fact]
        public async Task CreatingManagementClientAndMakingCallShouldSetHeadersCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleAssetManagement.json");
            var userAgent = "";
            var authHeader = "";
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                userAgent = request.Headers.GetValues("X-Contentful-User-Agent").First();
                authHeader = request.Headers.GetValues("Authorization").First();
            };
            var version = typeof(ContentfulClientBase).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            .InformationalVersion;

            //Act
            await _client.GetAssetAsync("sdf");

            //Assert
            Assert.Equal("Bearer 564", authHeader);
            Assert.StartsWith($"sdk contentful.csharp/{version}", userAgent);
        }
        
        [Fact]
        public async Task CreateContentTypeShouldSerializeRequestCorrectly()
        {
            //Arrange
            _handler.Response = new HttpResponseMessage() {
                Content = new StringContent("{}")
            };
            var contentType = new ContentType()
            {
                SystemProperties = new SystemProperties()
            };
            contentType.SystemProperties.Id = "123";
            contentType.Name = "Name";
            contentType.Fields = new List<Field>()
            {
                new Field()
                {
                    Name = "Field1",
                    Id = "field1",
                    @Type = "Symbol",
                    Required = true,
                    Localized = false,
                    Disabled = false,
                    Omitted = false
                },
                new Field()
                {
                    Name = "Field2",
                    Id = "field2",
                    @Type = "Location",
                    Required = false,
                    Localized = true,
                    Disabled = false,
                    Omitted = false
                },
                new Field()
                {
                    Name = "Field3",
                    Id = "field3",
                    @Type = "Text",
                    Required = false,
                    Localized = false,
                    Disabled = true,
                    Omitted = false,
                    Validations = new List<IFieldValidator>()
                    {
                        new SizeValidator(3,100)
                    }
                },
                new Field()
                {
                    Name = "Field4",
                    Id = "field4",
                    @Type = "Link",
                    Required = false,
                    Localized = false,
                    Disabled = false,
                    Omitted = false,
                    LinkType = "Asset"
                }
            };


            //Act
            var res = await _client.CreateOrUpdateContentTypeAsync(contentType);

            //Assert
            Assert.Null(res.Name);
        }

        [Fact]
        public async Task EditorInterfaceShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"EditorInterface.json");

            //Act
            var res = await _client.GetEditorInterfaceAsync("someid");

            //Assert
            Assert.Equal(7, res.Controls.Count);
            Assert.IsType<BooleanEditorInterfaceControlSettings>(res.Controls[4].Settings);
        }

        [Fact]
        public async Task CreateSpaceShouldCreateCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSpace.json");
            var headerSet = false;
            var contentSet = "";
            _handler.VerificationBeforeSend = () =>
            {
                 headerSet = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Organization").First() == "112";
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateSpaceAsync("Spaceman", "en-US", "112");

            //Assert
            Assert.True(headerSet);
            Assert.Equal(@"{""name"":""Spaceman"",""defaultLocale"":""en-US""}", contentSet);
            Assert.Equal("Products", res.Name);
        }

        [Fact]
        public async Task UpdateSpaceShouldCreateCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSpace.json");
            var headerSet = false;
            var contentSet = "";
            _handler.VerificationBeforeSend = () =>
            {
                headerSet = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First() == "37";
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.UpdateSpaceNameAsync("spaceId", "Spacemaster", 37, "333");

            //Assert
            Assert.True(headerSet);
            Assert.Equal(@"{""name"":""Spacemaster""}", contentSet);
            Assert.Equal("Products", res.Name);
        }

        [Theory]
        [InlineData("123")]
        [InlineData("54")]
        [InlineData("345")]
        public async Task DeletingASpaceShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            await _client.DeleteSpaceAsync(id);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/{id}", requestUrl);
        }

        [Fact]
        public async Task GetContentTypesShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"ContenttypesCollectionManagement.json");
            //Act
            var res = await _client.GetContentTypesAsync();

            //Assert
            Assert.Equal(4, res.Count());
            Assert.Equal("someName", res.First().Name);
            Assert.Equal(8, (res.First().Fields.First().Validations.First() as SizeValidator).Max);
        }

        [Fact]
        public async Task CreateOrUpdateContentTypeShouldThrowIfNoIdSet()
        {
            //Arrange
            var contentType = new ContentType()
            {
                Name = "Barbossa"
            };
            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.CreateOrUpdateContentTypeAsync(contentType));

            //Assert
            Assert.Equal($"The id of the content type must be set.{Environment.NewLine}Parameter name: contentType", ex.Message);
        }

        [Fact]
        public async Task CreateOrUpdateContentTypeShouldCreateCorrectObject()
        {
            //Arrange
            var contentType = new ContentType()
            {
                Name = "Barbossa",
                SystemProperties = new SystemProperties()
            };
            contentType.SystemProperties.Id = "323";
            _handler.Response = GetResponseFromFile(@"SampleContentType.json");

            var versionHeader = "";
            var contentSet = "";
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateOrUpdateContentTypeAsync(contentType, version: 22);

            //Assert
            Assert.Equal("22", versionHeader);
            Assert.Contains(@"""name"":""Barbossa""", contentSet);
        }

        [Fact]
        public async Task GetContentTypeShouldSerializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleContentType.json");

            //Act
            var res = await _client.GetContentTypeAsync("someid");

            //Assert
            Assert.Equal("Product", res.Name);
            Assert.Equal("productName", res.DisplayField);
            Assert.Equal(12, res.Fields.Count);
            Assert.True(res.Fields[0].Localized);
            Assert.Equal("Description", res.Fields[2].Name);
            Assert.Equal("Link", res.Fields[4].Items.Type);

        }

        [Fact]
        public async Task GetContentTypeShouldThrowForEmptyId()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleContentType.json");

            //Act
            var res = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetContentTypeAsync(""));

            //Assert
            Assert.Equal("contentTypeId", res.Message);
        }

        [Theory]
        [InlineData("777")]
        [InlineData("456")]
        [InlineData("666")]
        public async Task DeletingAContentTypeShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            await _client.DeleteContentTypeAsync(id);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/content_types/{id}", requestUrl);
        }

        [Fact]
        public async Task ActivatingContentTypeShouldThrowForEmptyId()
        {
            //Arrange
            
            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.ActivateContentTypeAsync("", 34));

            //Assert
            Assert.Equal("contentTypeId", ex.Message);
        }

        [Fact]
        public async Task ActivatingContentTypeShouldCallCorrectUrl()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleContentType.json");

            var versionHeader = "";
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            var res = await _client.ActivateContentTypeAsync("758", 345);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/content_types/758/published", requestUrl);
            Assert.Equal("345", versionHeader);
        }

        [Fact]
        public async Task DeactivatingContentTypeShouldThrowForEmptyId()
        {
            //Arrange

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.DeactivateContentTypeAsync(""));

            //Assert
            Assert.Equal("contentTypeId", ex.Message);
        }

        [Fact]
        public async Task DeactivatingContentTypeShouldCallCorrectUrl()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleContentType.json");

            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;

            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            await _client.DeactivateContentTypeAsync("324");

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/content_types/324/published", requestUrl);
        }

        [Fact]
        public async Task GetActivatedContentTypesShouldSerializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"ContenttypesCollectionManagement.json");
            var requestUrl = "";

            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            var res = await _client.GetActivatedContentTypesAsync();

            //Assert
            Assert.Equal(4, res.Count());
            Assert.Equal($"https://api.contentful.com/spaces/666/public/content_types", requestUrl);
            Assert.Equal("someName", res.First().Name);
            Assert.Equal(8, (res.First().Fields.First().Validations.First() as SizeValidator).Max);
        }

        [Fact]
        public async Task GetEditorInterfaceShouldThrowForEmptyId()
        {
            //Arrange

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetEditorInterfaceAsync(""));
            //Assert
            Assert.Equal("contentTypeId", ex.Message);
        }

        [Fact]
        public async Task GetEditorInterfaceShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"EditorInterface.json");

            //Act
            var res = await _client.GetEditorInterfaceAsync("23");
            //Assert
            Assert.Equal(7, res.Controls.Count);
            Assert.IsType<BooleanEditorInterfaceControlSettings>(res.Controls[4].Settings);
            Assert.IsType<RatingEditorInterfaceControlSettings>(res.Controls[5].Settings);
            Assert.Equal(7, (res.Controls[5].Settings as RatingEditorInterfaceControlSettings).NumberOfStars);
            Assert.Equal("How many do you likez?", res.Controls[5].Settings.HelpText);
            Assert.IsType<DatePickerEditorInterfaceControlSettings>(res.Controls[6].Settings);
            Assert.Equal(EditorInterfaceDateFormat.time, (res.Controls[6].Settings as DatePickerEditorInterfaceControlSettings).DateFormat);

        }

        [Fact]
        public async Task UpdateEditorInterfaceShouldThrowForEmptyId()
        {
            //Arrange
            var editorInterface = new EditorInterface();
            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.UpdateEditorInterfaceAsync(editorInterface, "", 6));
            //Assert
            Assert.Equal("contentTypeId", ex.Message);
        }

        [Fact]
        public async Task UpdateEditorInterfaceShouldCallCorrectUrl()
        {
            //Arrange
            var editorInterface = new EditorInterface()
            {
                Controls = new List<EditorInterfaceControl>()
            {
                new EditorInterfaceControl()
                {
                    FieldId = "field1",
                    WidgetId = SystemWidgetIds.SingleLine
                },
                new EditorInterfaceControl()
                {
                    FieldId = "field2",
                    WidgetId = SystemWidgetIds.Boolean,
                    Settings = new BooleanEditorInterfaceControlSettings()
                    {
                        HelpText = "Help me here!",
                        TrueLabel = "Truthy",
                        FalseLabel = "Falsy"
                    }
                }
            }
            };
            var versionHeader = "";
            var contentSet = "";
            var requestUrl = "";
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                requestUrl = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };
            _handler.Response = GetResponseFromFile(@"EditorInterface.json");

            //Act
            var res = await _client.UpdateEditorInterfaceAsync(editorInterface, "123", 16);

            //Assert
            Assert.Equal("16", versionHeader);
            Assert.Equal("https://api.contentful.com/spaces/666/content_types/123/editor_interface", requestUrl);
            Assert.Equal(@"{""controls"":[{""fieldId"":""field1"",""widgetId"":""singleLine""},{""fieldId"":""field2"",""widgetId"":""boolean"",""settings"":{""trueLabel"":""Truthy"",""falseLabel"":""Falsy"",""helpText"":""Help me here!""}}]}", contentSet);
        }

        [Fact]
        public async Task EditorInterfaceShouldSerializeCorrectly()
        {
            //Arrange
            var editorInterface = new EditorInterface()
            {
                Controls = new List<EditorInterfaceControl>()
            {
                new EditorInterfaceControl()
                {
                    FieldId = "field1",
                    WidgetId = SystemWidgetIds.SingleLine
                },
                new EditorInterfaceControl()
                {
                    FieldId = "field2",
                    WidgetId = SystemWidgetIds.Boolean,
                    Settings = new BooleanEditorInterfaceControlSettings()
                    {
                        HelpText = "Help me here!",
                        TrueLabel = "Truthy",
                        FalseLabel = "Falsy"
                    }
                }
            }
            };
            _handler.Response = GetResponseFromFile(@"EditorInterface.json");

            //Act
            var res = await _client.UpdateEditorInterfaceAsync(editorInterface, "123", 1);

            //Assert
            Assert.Equal(7, res.Controls.Count);
            Assert.IsType<BooleanEditorInterfaceControlSettings>(res.Controls[4].Settings);
        }

        [Fact]
        public async Task GetEntriesCollectionShouldSerializeIntoCorrectCollection()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"EntriesCollectionManagement.json");

            //Act
            var res = await _client.GetEntriesCollectionAsync<Entry<dynamic>>();

            //Assert
            Assert.Equal(8, res.Total);
            Assert.Equal(100, res.Limit);
            Assert.Equal(0, res.Skip);
            Assert.Equal(8, res.Items.Count());
            Assert.Equal("Somethi", res.First().Fields.field1["en-US"].ToString());
            Assert.Equal(DateTime.Parse("2016-11-23T09:40:56.857Z").ToUniversalTime(), res.First().SystemProperties.CreatedAt);
            Assert.Equal("testagain", res.First().SystemProperties.ContentType.SystemProperties.Id);
        }

        [Fact]
        public async Task CreateOrUpdateEntryShouldThrowIfIdIsNotSet()
        {
            //Arrange
            var entry = new Entry<dynamic>()
            {
                SystemProperties = new SystemProperties()
            };
            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.CreateOrUpdateEntryAsync(entry, contentTypeId: "Hwoarang"));
            //Assert
            Assert.Equal("The id of the entry must be set.", ex.Message);
        }

        [Fact]
        public async Task CreateOrUpdateEntryShouldAddCorrectContentTypeHeader()
        {
            //Arrange
            var entry = new Entry<dynamic>()
            {
                SystemProperties = new SystemProperties()
            };
            entry.SystemProperties.Id = "123";
            var contentTypeHeader = "";
            _handler.VerificationBeforeSend = () =>
            {
                contentTypeHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Content-Type").First();
            };
            _handler.Response = GetResponseFromFile(@"SampleEntryManagement.json");

            //Act
            var res = await _client.CreateOrUpdateEntryAsync(entry, contentTypeId: "Eddie Gordo");
            //Assert
            Assert.Equal("Eddie Gordo", contentTypeHeader);
        }

        [Fact]
        public async Task CreateOrUpdateEntryShouldNotAddContentTypeHeaderIfNotSet()
        {
            //Arrange
            var entry = new Entry<dynamic>()
            {
                SystemProperties = new SystemProperties()
            };
            entry.SystemProperties.Id = "123";
            IEnumerable<string> contentTypeHeader = new List<string>();
            _handler.VerificationBeforeSend = () =>
            {
                _httpClient.DefaultRequestHeaders.TryGetValues("X-Contentful-Content-Type", out contentTypeHeader);
            };
            _handler.Response = GetResponseFromFile(@"SampleEntryManagement.json");
            //Act
            var res = await _client.CreateOrUpdateEntryAsync(entry, version: 7);
            //Assert
            Assert.Null(contentTypeHeader);
        }

        [Fact]
        public async Task CreateOrUpdateEntryShouldCallCorrectUrlWithData()
        {
            //Arrange
            var entry = new Entry<dynamic>()
            {
                SystemProperties = new SystemProperties()
            };
            entry.SystemProperties.Id = "532";
            entry.Fields = new ExpandoObject();
            entry.Fields.field34 = new Dictionary<string, string>()
            {
                { "en-US", "banana" }
            };
            var contentTypeHeader = "";
            var contentSet = "";
            var requestUrl = "";
            var versionHeader = "";
            _handler.VerificationBeforeSend = () =>
            {
                contentTypeHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Content-Type").First();
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                requestUrl = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };
            _handler.Response = GetResponseFromFile(@"SampleEntryManagement.json");

            //Act
            var res = await _client.CreateOrUpdateEntryAsync(entry, contentTypeId: "Bryan Fury", version: 45);
            //Assert
            Assert.Equal("Bryan Fury", contentTypeHeader);
            Assert.Equal("45", versionHeader);
            Assert.Equal("https://api.contentful.com/spaces/666/entries/532", requestUrl);
            Assert.Contains(@"""field34"":{""en-US"":""banana""}", contentSet);
        }

        [Fact]
        public async Task CreateEntryShouldCallCorrectUrlWithData()
        {
            //Arrange
            var entry = new Entry<dynamic>();

#pragma warning disable IDE0017 // Object initialization can be simplified
            entry.Fields = new ExpandoObject();
#pragma warning restore IDE0017 // Object initialization can be simplified
            entry.Fields.field34 = new Dictionary<string, string>()
            {
                { "en-US", "bapple" }
            };
            var contentTypeHeader = "";
            var contentSet = "";
            var requestUrl = "";
            _handler.VerificationBeforeSend = () =>
            {
                contentTypeHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Content-Type").First();
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                requestUrl = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };
            _handler.Response = GetResponseFromFile(@"SampleEntryManagement.json");

            //Act
            var res = await _client.CreateEntryAsync(entry, contentTypeId: "Ling Xiaoyu");
            //Assert
            Assert.Equal("Ling Xiaoyu", contentTypeHeader);
            Assert.Equal("https://api.contentful.com/spaces/666/entries", requestUrl);
            Assert.Contains(@"""field34"":{""en-US"":""bapple""}", contentSet);
        }

        [Fact]
        public async Task CreateEntryShouldThrowIfContentTypeIsNotSet()
        {
            //Arrange
            var entry = new Entry<dynamic>();

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.CreateEntryAsync(entry, contentTypeId: ""));
            //Assert
            Assert.Equal($"The content type id must be set.{Environment.NewLine}Parameter name: contentTypeId", ex.Message);
        }

        [Fact]
        public async Task GetEntryShouldThrowIfIdIsNotSet()
        {
            //Arrange

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetEntryAsync(""));
            //Assert
            Assert.Equal("entryId", ex.Message);
        }

        [Fact]
        public async Task GetEntryShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleEntryManagement.json");

            //Act
            var res = await _client.GetEntryAsync("43");
            //Assert
            Assert.Equal("42BwcCt4TeeskG0eq8S2CQ", res.SystemProperties.Id);
            Assert.Equal("Somethi", res.Fields.field1["en-US"].ToString());
        }

        [Theory]
        [InlineData("777")]
        [InlineData("456")]
        [InlineData("666")]
        public async Task DeletingAnEntryShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.DeleteEntryAsync(id, 32);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal("32", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/entries/{id}", requestUrl);
        }

        [Theory]
        [InlineData("777")]
        [InlineData("abc")]
        [InlineData("666")]
        public async Task PublishingAnEntryShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.Response = GetResponseFromFile(@"SampleEntryManagement.json");

            //Act
            await _client.PublishEntryAsync(id, 23);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("23", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/entries/{id}/published", requestUrl);
        }

        [Theory]
        [InlineData("777")]
        [InlineData("abc")]
        [InlineData("666")]
        public async Task UnpublishingAnEntryShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.Response = GetResponseFromFile(@"SampleEntryManagement.json");

            //Act
            await _client.PublishEntryAsync(id, 23);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("23", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/entries/{id}/published", requestUrl);
        }

        [Theory]
        [InlineData("777")]
        [InlineData("abc")]
        [InlineData("666")]
        public async Task ArchivingAnEntryShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.Response = GetResponseFromFile(@"SampleEntryManagement.json");

            //Act
            await _client.ArchiveEntryAsync(id, 78);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("78", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/entries/{id}/archived", requestUrl);
        }

        [Theory]
        [InlineData("777")]
        [InlineData("abc")]
        [InlineData("666")]
        public async Task UnarchivingAnEntryShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.Response = GetResponseFromFile(@"SampleEntryManagement.json");

            //Act
            await _client.UnarchiveEntryAsync(id, 67);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal("67", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/entries/{id}/archived", requestUrl);
        }

        [Fact]
        public async Task GetAllAssetsShouldReturnCorrectCollection()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"AssetsCollectionManagement.json");

            //Act
            var res = await _client.GetAssetsCollectionAsync();

            //Assert
            Assert.Equal(7, res.Count());
            Assert.Equal(7, res.Total);
            Assert.Equal("Ernest Hemingway (1950)", res.First().Title["en-US"]);
            Assert.Equal(2290561, res.First().Files["en-US"].Details.Size);
            Assert.Equal(2940, res.First().Files["en-US"].Details.Image.Width);
            Assert.Equal("Alice in Wonderland", res.Last().Title["en-US"]);
        }

        [Theory]
        [InlineData("?fields.title=bam")]
        [InlineData("?fields.description[exists]=false")]
        [InlineData("?order=sys.createdAt")]
        public async Task GetAllAssetsWithQueryShouldCallCorrectUrl(string query)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"AssetsCollectionManagement.json");

            var requestUrl = "";
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            var res = await _client.GetAssetsCollectionAsync(query);

            //Assert
            Assert.Equal(7, res.Count());
            Assert.Equal(7, res.Total);
            Assert.Equal("Ernest Hemingway (1950)", res.First().Title["en-US"]);
            Assert.Equal(2290561, res.First().Files["en-US"].Details.Size);
            Assert.Equal(2940, res.First().Files["en-US"].Details.Image.Width);
            Assert.Equal("Alice in Wonderland", res.Last().Title["en-US"]);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{query}", requestUrl);
        }

        [Fact]
        public async Task CreateAssetShouldThrowIfIdNotSet()
        {
            //Arrange
            var asset = new ManagementAsset()
            {
                Title = new Dictionary<string, string>()
            };
            asset.Title["en-US"] = "Burton Green";

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.CreateOrUpdateAssetAsync(asset));
            //Assert
            Assert.Equal("The id of the asset must be set.", ex.Message);
        }

        [Fact]
        public async Task CreateAssetShouldCallCorrectUrlWithCorrectData()
        {
            //Arrange
            var asset = new ManagementAsset()
            {
                Title = new Dictionary<string, string>()
            };
            asset.Title["en-US"] = "Burton Green";
            asset.SystemProperties = new SystemProperties()
            {
                Id = "424"
            };
            _handler.Response = GetResponseFromFile(@"SampleAssetManagement.json");
            var requestUrl = "";
            var contentSet = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            var res = await _client.CreateOrUpdateAssetAsync(asset);
            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Contains(@"""en-US"":""Burton Green""", contentSet);
            Assert.Equal("https://api.contentful.com/spaces/666/assets/424", requestUrl);
        }

        [Fact]
        public async Task CreateAssetShouldSetVersionHeaderCorrectly()
        {
            //Arrange
            var asset = new ManagementAsset()
            {
                Title = new Dictionary<string, string>()
            };
            asset.Title["en-US"] = "Burton Green";
            asset.SystemProperties = new SystemProperties()
            {
                Id = "424"
            };
            _handler.Response = GetResponseFromFile(@"SampleAssetManagement.json");
            var versionHeader = "";
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            var res = await _client.CreateOrUpdateAssetAsync(asset, version: 234);
            //Assert
            Assert.Equal("234", versionHeader);
        }

        [Fact]
        public async Task GetAllPublishedAssetsShouldReturnCorrectCollection()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"AssetsCollectionManagement.json");

            //Act
            var res = await _client.GetPublishedAssetsCollectionAsync();

            //Assert
            Assert.Equal(7, res.Count());
            Assert.Equal(7, res.Total);
            Assert.Equal("Ernest Hemingway (1950)", res.First().Title["en-US"]);
            Assert.Equal(2290561, res.First().Files["en-US"].Details.Size);
            Assert.Equal(2940, res.First().Files["en-US"].Details.Image.Width);
            Assert.Equal("Alice in Wonderland", res.Last().Title["en-US"]);
        }

        [Fact]
        public async Task GetAssetShouldThrowIfNoIdSet()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleAssetManagement.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetAssetAsync(""));

            //Assert
            Assert.Equal("assetId", ex.Message);
        }

        [Fact]
        public async Task GetAssetShouldReturnCorrectItem()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleAssetManagement.json");

            //Act
            var res = await _client.GetAssetAsync("234");

            //Assert
            Assert.Equal("Ernest Hemingway (1950)", res.Title["en-US"]);
            Assert.Equal("Hemingway in the cabin of his boat Pilar, off the coast of Cuba", res.Description["en-US"]);
        }

        [Theory]
        [InlineData("7HG7")]
        [InlineData("asf")]
        [InlineData("666")]
        public async Task DeletingAnAssetShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.DeleteAssetAsync(id, 86);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal("86", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}", requestUrl);
        }

        [Theory]
        [InlineData("7HG7", "en-US")]
        [InlineData("asf", "sv")]
        [InlineData("g3445g", "cucumber")]
        public async Task ProcessingAnAssetShouldCallCorrectUrl(string id, string locale)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.ProcessAssetAsync(id, 25, locale);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("25", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}/files/{locale}/process", requestUrl);
        }

        [Theory]
        [InlineData("7HG7")]
        [InlineData("asf")]
        [InlineData("666")]
        public async Task PublishingAnAssetShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleAssetManagement.json");
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.PublishAssetAsync(id, 23);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("23", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}/published", requestUrl);
        }

        [Theory]
        [InlineData("7HG7")]
        [InlineData("ab45")]
        [InlineData("666")]
        public async Task UnpublishingAnAssetShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleAssetManagement.json");
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.UnpublishAssetAsync(id, 38);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal("38", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}/published", requestUrl);
        }

        [Theory]
        [InlineData("asg")]
        [InlineData("435")]
        [InlineData("785685fgh")]
        public async Task ArchivingAnAssetShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleAssetManagement.json");
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.ArchiveAssetAsync(id, 23);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("23", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}/archived", requestUrl);
        }

        [Theory]
        [InlineData("57y43hr")]
        [InlineData("346w")]
        [InlineData("babt3")]
        public async Task UnarchivingAnAssetShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleAssetManagement.json");
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.UnarchiveAssetAsync(id, 89);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal("89", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}/archived", requestUrl);
        }

        [Fact]
        public async Task GetAllLocalesShouldReturnCorrectCollection()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"LocalesCollection.json");

            //Act
            var res = await _client.GetLocalesCollectionAsync();

            //Assert
            Assert.Equal(1, res.Count());
            Assert.Equal(1, res.Total);
            Assert.Equal("U.S. English", res.First().Name);
            Assert.Equal("en-US", res.First().Code);
            Assert.True(res.First().Default);
            Assert.Null(res.First().FallbackCode);
        }

        [Fact]
        public async Task CreateLocaleShouldCreateCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleLocale.json");
            var locale = new Locale()
            {
                Name = "Unintelligible English",
                FallbackCode = "sv-SE",
                Optional = true,
                Code = "en-UI"
            };
            var contentSet = "";
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateLocaleAsync(locale);

            //Assert
            Assert.Equal(@"{""code"":""en-UI"",""contentDeliveryApi"":false,""contentManagementApi"":false,""fallbackCode"":""sv-SE"",""name"":""Unintelligible English"",""optional"":true}", contentSet);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetLocaleShouldThrowWhenIdIsNotSet(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleLocale.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetLocaleAsync(id));

            //Assert
            Assert.Equal("The localeId must be set.", ex.Message);
        }

        [Fact]
        public async Task GetLocaleShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleLocale.json");

            //Act
            var res = await _client.GetLocaleAsync("art");

            //Assert
            Assert.Equal("U.S. English", res.Name);
            Assert.Equal("en-US", res.Code);
        }

        [Theory]
        [InlineData("asg")]
        [InlineData("21345")]
        [InlineData("hgf633f")]
        public async Task DeletingLocaleShouldCallCorrectUrl(string id)
        {
            //Arrange
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.Response = new HttpResponseMessage();
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };


            //Act
            await _client.DeleteLocaleAsync(id);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/locales/{id}", requestUrl);
        }

        [Fact]
        public async Task GetAllWebHooksShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"WebhookCollection.json");

            //Act
            var res = await _client.GetWebHooksCollectionAsync();

            //Assert
            Assert.Equal(1, res.Total);
            Assert.Equal(1, res.Count());
            Assert.Equal("Testhook", res.First().Name);
            Assert.Equal("https://robertlinde.se/", res.First().Url);
        }

        [Fact]
        public async Task CreateWebHookShouldCallCorrectUrlWithData()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleWebHook.json");

            var webhook = new WebHook()
            {
                Name = "Some hook",
                Url = "https://www.cracked.com/",
                HttpBasicPassword = "Tepes",
                HttpBasicUsername = "Vlad",
                Topics = new List<string>()
            {
                "Entry.create",
                "Asset.publish"
            }
            };
            var contentSet = "";
            var url = "";
            var method = HttpMethod.Trace;
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                method = request.Method;
                url = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateWebHookAsync(webhook);

            //Assert
            Assert.Equal(HttpMethod.Post, method);
            Assert.Equal("https://api.contentful.com/spaces/666/webhook_definitions", url);
            Assert.Contains(@"""name"":""Some hook""", contentSet);
            Assert.Contains(@"""url"":""https://www.cracked.com/""", contentSet);
            Assert.Contains(@"""httpBasicUsername"":""Vlad""", contentSet);
            Assert.Contains(@"""httpBasicPassword"":""Tepes""", contentSet);
            Assert.Contains(@"""Entry.create""", contentSet);
            Assert.Contains(@"""Asset.publish""", contentSet);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task CreateOrUpdateWebHookShouldThrowIfNoIdSet(string id)
        {
            //Arrange
            var webHook = new WebHook()
            {
                SystemProperties = new SystemProperties()
            };
            webHook.SystemProperties.Id = id;

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.CreateOrUpdateWebHookAsync(webHook));

            //Assert
            Assert.Equal("The id of the webhook must be set.", ex.Message);
        }

        [Theory]
        [InlineData("2354")]
        [InlineData("33")]
        [InlineData("vadfb#¤123")]
        public async Task CreateOrUpdateWebHookShouldCallCorrectUrlWithData(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleWebHook.json");

            var webhook = new WebHook()
            {
                SystemProperties = new SystemProperties()
            };
            webhook.SystemProperties.Id = id;
            webhook.Name = "Bingo";
            webhook.Url = "http://www.imdb.com/name/nm0001159/";
            webhook.HttpBasicPassword = "Caligula";
            webhook.HttpBasicUsername = "Emperor";
            webhook.Topics = new List<string>()
            {
                "Asset.create",
                "Entry.*"
            };
            var contentSet = "";
            var url = "";
            var method = HttpMethod.Trace;
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                method = request.Method;
                url = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateOrUpdateWebHookAsync(webhook);

            //Assert
            Assert.Equal(HttpMethod.Put, method);
            Assert.Equal($"https://api.contentful.com/spaces/666/webhook_definitions/{id}", url);
            Assert.Contains(@"""name"":""Bingo""", contentSet);
            Assert.Contains(@"""url"":""http://www.imdb.com/name/nm0001159/""", contentSet);
            Assert.Contains(@"""httpBasicUsername"":""Emperor""", contentSet);
            Assert.Contains(@"""httpBasicPassword"":""Caligula""", contentSet);
            Assert.Contains(@"""Asset.create""", contentSet);
            Assert.Contains(@"""Entry.*""", contentSet);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetWebHookShouldThrowIfNoIdSet(string id)
        {
            //Arrange


            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetWebHookAsync(id));

            //Assert
            Assert.Equal($"The id of the webhook must be set.{Environment.NewLine}Parameter name: webhookId", ex.Message);
        }

        [Fact]
        public async Task GetWebHookShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleWebHook.json");

            //Act
            var res = await _client.GetWebHookAsync("ertg");

            //Assert
            Assert.Equal("Testhook", res.Name);
            Assert.Equal("https://robertlinde.se/", res.Url);
            Assert.Collection(res.Topics, 
                (t) => Assert.Equal("Asset.archive", t),
                (t) => Assert.Equal("Asset.unarchive", t),
                (t) => Assert.Equal("ContentType.create", t),
                (t) => Assert.Equal("ContentType.save", t),
                (t) => Assert.Equal("Entry.publish", t),
                (t) => Assert.Equal("Entry.unpublish", t));
            Assert.Collection(res.Headers, (h) => { Assert.Equal("bob", h.Key); Assert.Equal("uncle", h.Value); });
        }

        [Theory]
        [InlineData("346")]
        [InlineData("yw345")]
        [InlineData("xcb345af")]
        public async Task DeletingWebhookShouldCallCorrectUrl(string id)
        {
            //Arrange
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.Response = new HttpResponseMessage();
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            await _client.DeleteWebHookAsync(id);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/webhook_definitions/{id}", requestUrl);
        }

        [Fact]
        public async Task WebHookCallDetailsShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"WebhookCallDetails.json");
            //Act
            var res = await _client.GetWebHookCallDetailsAsync("b", "s");

            //Assert
            Assert.Equal("unarchive", res.EventType);
            Assert.Equal("close", res.Response.Headers["connection"]);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task WebHookCallDetailShouldThrowForWebHookIdNotSet(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"WebhookCallDetails.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetWebHookCallDetailsAsync("some", id));

            //Assert
            Assert.Equal($"The id of the webhook must be set.{Environment.NewLine}Parameter name: webhookId", ex.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task WebHookCallDetailShouldThrowForWebHookCallIdNotSet(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"WebhookCallDetails.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetWebHookCallDetailsAsync(id, "some"));

            //Assert
            Assert.Equal($"The id of the webhook call must be set.{Environment.NewLine}Parameter name: callId", ex.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task WebHookCallDetailsShouldThrowForIdNotSet(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"WebhookCallDetailsCollection.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetWebHookCallDetailsCollectionAsync(id));

            //Assert
            Assert.Equal($"The id of the webhook must be set.{Environment.NewLine}Parameter name: webhookId", ex.Message);
        }

        [Fact]
        public async Task WebHookCallDetailsCollectionShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"WebhookCallDetailsCollection.json");

            //Act
            var res = await _client.GetWebHookCallDetailsCollectionAsync("aaf");

            //Assert
            Assert.Equal(2, res.Total);
            Assert.Equal(2, res.Count());
            Assert.Equal(403, res.First().StatusCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task WebHookHealthShouldThrowForIdNotSet(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"WebhookHealth.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetWebHookHealthAsync(id));

            //Assert
            Assert.Equal($"The id of the webhook must be set.{Environment.NewLine}Parameter name: webhookId", ex.Message);
        }

        [Fact]
        public async Task WebHookHealthShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"WebhookHealth.json");

            //Act
            var res = await _client.GetWebHookHealthAsync("aaf");

            //Assert
            Assert.Equal(2, res.TotalCalls);
            Assert.Equal(0, res.TotalHealthy);
        }

        [Fact]
        public void ConstraintsShouldSerializeCorrectly()
        {
            //Arrange
            var policy = new Policy()
            {
                Effect = "allow",
                Actions = new List<string>()
            {
                "create",
                "delete"
            },
                Constraint =
                new AndConstraint()
                {
                    new EqualsConstraint()
                    {
                        Property = "sys.type",
                        ValueToEqual = "Entry"
                    },
                    new EqualsConstraint()
                    {
                        Property = "sys.createdBy.sys.id",
                        ValueToEqual = "User.current()"
                    },
                    new NotConstraint()
                    {
                        ConstraintToInvert = new EqualsConstraint()
                        {
                            Property = "sys.contentType.sys.id",
                            ValueToEqual = "123"
                        }
                    }
            }
            };

            //Act
            var output = JsonConvert.SerializeObject(policy, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            //Assert
            Assert.Equal(@"{""effect"":""allow"",""actions"":[""create"",""delete""],""constraint"":{""and"":[{""equals"":[{""doc"":""sys.type""},""Entry""]},{""equals"":[{""doc"":""sys.createdBy.sys.id""},""User.current()""]},{""not"":{""equals"":[{""doc"":""sys.contentType.sys.id""},""123""]}}]}}", output);
        }

        [Fact]
        public async Task RoleShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleRole.json");

            //Act
            var res = await _client.GetRoleAsync("123");

            //Assert
            Assert.Equal("Developer", res.Name);
            Assert.Equal("sys.type", ((res.Policies[1].Constraint as AndConstraint)[0] as EqualsConstraint).Property);
            Assert.Equal("Asset", ((res.Policies[1].Constraint as AndConstraint)[0] as EqualsConstraint).ValueToEqual);
        }

        [Fact]
        public async Task RoleShouldSerializeCorrectly()
        {
            //Arrange
            var role = new Role();
            _handler.Response = GetResponseFromFile(@"SampleRole.json");


            role.Name = "test";
            role.Description = "desc";
            role.Permissions = new ContentfulPermissions()
            {
                ContentDelivery = new List<string>() { "all" },
                ContentModel = new List<string>() { "read" },
                Settings = new List<string>() { "read", "manage" }
            };
            role.Policies = new List<Policy>
            {
                new Policy()
                {
                    Effect = "allow",
                    Actions = new List<string>()
                {
                    "read",
                    "create",
                    "update"
                },
                    Constraint = new AndConstraint()
                {
                    new EqualsConstraint()
                    {
                        Property = "sys.type",
                        ValueToEqual = "Entry"
                    }
                }
                }
            };

            //Act
            var res = await _client.CreateRoleAsync(role);

            //Assert
            Assert.Equal("Developer", res.Name);
            Assert.Equal("sys.type", ((res.Policies[1].Constraint as AndConstraint)[0] as EqualsConstraint).Property);
            Assert.Equal("Asset", ((res.Policies[1].Constraint as AndConstraint)[0] as EqualsConstraint).ValueToEqual);
        }

        [Fact]
        public async Task RolesCollectionShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleRolesCollection.json");

            //Act
            var res = await _client.GetAllRolesAsync();

            //Assert
            Assert.Equal(7, res.Total);
            Assert.Equal("Author", res.First().Name);
            Assert.Equal("create", res.First().Policies.First().Actions[0]);
            Assert.Equal("Translator 1", res.ElementAt(4).Name);
            Assert.Equal("Entry", ((res.ElementAt(4).Policies.First().Constraint as AndConstraint).First() as EqualsConstraint).ValueToEqual);
        }

        [Fact]
        public async Task CreateRoleShouldCallCorrectUrlWithData()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleRole.json");

            var role = new Role()
            {
                Name = "Da role",
                Description = "Descriptive stuff",
                Permissions = new ContentfulPermissions()
                {
                    ContentDelivery = new List<string>()
                {
                    "all"
                },
                    ContentModel = new List<string>()
                {
                    "read"
                },
                    Settings = new List<string>()
                {
                    "read",
                    "manage"
                }
                }
            };
            var contentSet = "";
            var url = "";
            var method = HttpMethod.Trace;
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                method = request.Method;
                url = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateRoleAsync(role);

            //Assert
            Assert.Equal(HttpMethod.Post, method);
            Assert.Equal("https://api.contentful.com/spaces/666/roles", url);
            Assert.Contains(@"""name"":""Da role""", contentSet);
            Assert.Contains(@"""description"":""Descriptive stuff""", contentSet);
            Assert.Contains(@"""ContentDelivery"":""all""", contentSet);
            Assert.Contains(@"""ContentModel"":[""read""]", contentSet);
            Assert.Contains(@"""Settings"":[""read"",""manage""]", contentSet);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task UpdateRoleShouldThrowForIdNotSet(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleRole.json");
            var role = new Role()
            {
                SystemProperties = new SystemProperties()
            };
            role.SystemProperties.Id = id;
            
            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.UpdateRoleAsync(role));

            //Assert
            Assert.Equal("The id of the role must be set.", ex.Message);
        }

        [Theory]
        [InlineData("agsdg455")]
        [InlineData("324")]
        [InlineData("bcvb")]
        public async Task UpdateRoleShouldCallCorrectUrlWithData(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleRole.json");

            var role = new Role()
            {
                SystemProperties = new SystemProperties()
                {
                    Id = id
                },
                Name = "Rolemodel",
                Description = "Merry christmas!",
                Permissions = new ContentfulPermissions()
                {
                    ContentDelivery = new List<string>()
                {
                    "read","delete"
                },
                    ContentModel = new List<string>()
                {
                    "all"
                },
                    Settings = new List<string>()
                {
                    "read",
                    "manage"
                }
                }
            };
            var contentSet = "";
            var url = "";
            var method = HttpMethod.Trace;
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                method = request.Method;
                url = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.UpdateRoleAsync(role);

            //Assert
            Assert.Equal(HttpMethod.Put, method);
            Assert.Equal($"https://api.contentful.com/spaces/666/roles/{id}", url);
            Assert.Contains(@"""name"":""Rolemodel""", contentSet);
            Assert.Contains(@"""description"":""Merry christmas!""", contentSet);
            Assert.Contains(@"""ContentModel"":""all""", contentSet);
            Assert.Contains(@"""ContentDelivery"":[""read"",""delete""]", contentSet);
            Assert.Contains(@"""Settings"":[""read"",""manage""]", contentSet);
        }

        [Theory]
        [InlineData("09hfdh4-34")]
        [InlineData("643")]
        [InlineData("fdgs34")]
        public async Task DeletingRoleShouldCallCorrectUrl(string id)
        {
            //Arrange
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.Response = new HttpResponseMessage();
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            await _client.DeleteRoleAsync(id);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/roles/{id}", requestUrl);
        }

        [Fact]
        public async Task SnapshotsShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SnapshotsCollection.json");

            //Act
            var res = await _client.GetAllSnapshotsForEntryAsync("123");

            //Assert
            Assert.Equal("Seven Tips From Ernest Hemingway on How to Write Fiction", res.First().Fields["title"]["en-US"]);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetSnapshotShouldThrowForSnapshotIdNotSet(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSnapshot.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetSnapshotForEntryAsync(id, "something"));

            //Assert
            Assert.Equal($"The id of the snapshot must be set.{Environment.NewLine}Parameter name: snapshotId", ex.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetSnapshotShouldThrowForEntryIdNotSet(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSnapshot.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetSnapshotForEntryAsync("something", id));

            //Assert
            Assert.Equal($"The id of the entry must be set.{Environment.NewLine}Parameter name: entryId", ex.Message);
        }

        [Fact]
        public async Task GetSnapshotShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSnapshot.json");
            
            //Act
            var res = await _client.GetSnapshotForEntryAsync("123", "wed");

            //Assert
            Assert.Equal("Somethi", res.Fields["field1"]["en-US"]);
            Assert.Equal("2ReMHJhXoAcy4AyamgsgwQ", res.Fields["field4"]["en-US"].sys.id.ToString());
        }

        [Fact]
        public async Task GetSnapshotsForContentTypeShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"ContentTypeSnapshotsCollection.json");

            //Act
            var res = await _client.GetAllSnapshotsForContentTypeAsync("123");
            var list = res.ToList();

            //Assert
            Assert.Equal(3, list.Count);
            Assert.Equal(12, list.First().Snapshot.Fields.Count);
            Assert.Equal(13, list[1].Snapshot.Fields.Count);
            Assert.False(list[0].Snapshot.Fields.Any(c => c.Name == "test"));
            Assert.True(list[1].Snapshot.Fields.Any(c => c.Name == "test" && c.Omitted == true));
            Assert.True(list[2].Snapshot.Fields.Any(c => c.Name == "test" && c.Omitted == false));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetSnapshotsForContentTypeShouldThrowForIdNotSet(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"ContentTypeSnapshotsCollection.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetAllSnapshotsForContentTypeAsync(id));

            //Assert
            Assert.Equal($"The id of the content type must be set.{Environment.NewLine}Parameter name: contentTypeId", ex.Message);
        }

        [Theory]
        [InlineData("", "snap")]
        [InlineData(null, "snap")]
        [InlineData("cat", "")]
        [InlineData("dog", null)]
        public async Task GetSnapshotForContentTypeShouldThrowForIdNotSet(string id, string snapId)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSnapshotContentType.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetSnapshotForContentTypeAsync(snapId, id));

            //Assert
            Assert.Contains($"The id of the ", ex.Message);
        }

        [Fact]
        public async Task GetSnapshotForContentTypeShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSnapshotContentType.json");

            //Act
            var res = await _client.GetSnapshotForContentTypeAsync("123", "wed");

            //Assert
            Assert.Equal("Product", res.Snapshot.Name);
            Assert.Equal("productName", res.Snapshot.DisplayField);
            Assert.Equal(13, res.Snapshot.Fields.Count);
        }

        [Fact]
        public async Task SpaceMembershipsShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSpaceMembershipsCollection.json");

            //Act
            var res = await _client.GetSpaceMembershipsAsync();

            //Assert
            Assert.Equal(1, res.Total);
            Assert.True(res.First().Admin);
            Assert.Equal("123", res.First().Roles[1]);
        }

        [Fact]
        public async Task SpaceMembershipShouldSerializeCorrectly()
        {
            //Arrange
            var membership = new SpaceMembership()
            {
                Admin = true,
                Roles = new List<string>()
            {
                "123",
                "231",
                "12344"
            }
            };
            var serializedMembership = JsonConvert.SerializeObject(membership);
            _handler.Response = new HttpResponseMessage() { Content = new StringContent(serializedMembership) };

            //Act
            var res = await _client.CreateSpaceMembershipAsync(membership);

            //Assert
            Assert.True(res.Admin);
            Assert.Equal(3, res.Roles.Count);
            Assert.Equal("231", res.Roles[1]);
        }

        [Fact]
        public async Task CreateSpaceMembershipShouldCallCorrectUrlWithData()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSpaceMembershipsCollection.json");

            var spaceMembership = new SpaceMembership()
            {
                Admin = true,
                Roles = new List<string>()
            {
                "123",
                "342"
            }
            };
            var contentSet = "";
            var url = "";
            var method = HttpMethod.Trace;
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                method = request.Method;
                url = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateSpaceMembershipAsync(spaceMembership);

            //Assert
            Assert.Equal(HttpMethod.Post, method);
            Assert.Equal("https://api.contentful.com/spaces/666/space_memberships", url);
            Assert.Equal(@"{""admin"":true,""roles"":[{""type"":""Link"",""linkType"":""Role"",""id"":""123""},{""type"":""Link"",""linkType"":""Role"",""id"":""342""}]}", contentSet);

        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task UpdateSpaceMembershipShouldThrowForEntryIdNotSet(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSpaceMembershipsCollection.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetSnapshotForEntryAsync("something", id));

            //Assert
            Assert.Equal($"The id of the entry must be set.{Environment.NewLine}Parameter name: entryId", ex.Message);
        }

        [Theory]
        [InlineData("afas23")]
        [InlineData("234")]
        [InlineData("bbs")]
        public async Task UpdateSpaceMembershipShouldCallCorrectUrlWithData(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleSpaceMembershipsCollection.json");

            var spaceMembership = new SpaceMembership()
            {
                SystemProperties = new SystemProperties()
            };
            spaceMembership.SystemProperties.Id = id;
            spaceMembership.Admin = false;
            spaceMembership.Roles = new List<string>()
            {
                "43",
                "765"
            };
            var contentSet = "";
            var url = "";
            var method = HttpMethod.Trace;
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                method = request.Method;
                url = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.UpdateSpaceMembershipAsync(spaceMembership);

            //Assert
            Assert.Equal(HttpMethod.Put, method);
            Assert.Equal($"https://api.contentful.com/spaces/666/space_memberships/{id}", url);
            Assert.Equal(@"{""admin"":false,""roles"":[{""type"":""Link"",""linkType"":""Role"",""id"":""43""},{""type"":""Link"",""linkType"":""Role"",""id"":""765""}]}", contentSet);
        }

        [Theory]
        [InlineData("adf-2345")]
        [InlineData("453")]
        [InlineData("agf")]
        public async Task DeletingSpaceMembershipShouldCallCorrectUrl(string id)
        {
            //Arrange
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.Response = new HttpResponseMessage();
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            await _client.DeleteSpaceMembershipAsync(id);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/space_memberships/{id}", requestUrl);
        }

        [Fact]
        public async Task GetApiKeysShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"ApiKeysCollection.json");

            //Act
            var res = await _client.GetAllApiKeysAsync();

            //Assert
            Assert.Equal(2, res.Total);
            Assert.Equal(2, res.Count());
            Assert.Equal("123", res.First().AccessToken);
        }

        [Fact]
        public async Task CreateApiKeyShouldCallCorrectUrlWithData()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"ApiKeysCollection.json");
            var contentSet = "";
            var url = "";
            var method = HttpMethod.Trace;
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                method = request.Method;
                url = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateApiKeyAsync("Key sharp!", "This is the desc");

            //Assert
            Assert.Equal(HttpMethod.Post, method);
            Assert.Equal("https://api.contentful.com/spaces/666/api_keys", url);
            Assert.Equal(@"{""name"":""Key sharp!"",""description"":""This is the desc""}", contentSet);
        }

        [Fact]
        public async Task UploadingFileShouldYieldCorrectResult()
        {
            //Arrange
            var fileBytes = new byte[] { 12, 43, 43, 54 };
            _handler.Response = GetResponseFromFile(@"UploadResult.json");

            //Act
            var res = await _client.UploadFileAsync(fileBytes);

            //Assert
            Assert.IsType<UploadReference>(res);
            Assert.NotNull(res.SystemProperties.Id);
            Assert.Equal("666", res.SystemProperties.Id);
        }

        [Fact]
        public async Task GettingUploadedFileShouldYieldCorrectResult()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"UploadResult.json");

            //Act
            var res = await _client.GetUploadAsync("5wAqke81S2C1o32RvTODCl");

            Assert.IsType<UploadReference>(res);
            Assert.NotNull(res.SystemProperties.Id);
            Assert.Equal("666", res.SystemProperties.Id);
        }

        [Fact]
        public async Task GetExtensionsShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleExtensionsCollection.json");

            //Act
            var res = await _client.GetAllExtensionsAsync();

            //Assert
            Assert.Equal(1, res.Total);
            Assert.Equal(1, res.Count());
            Assert.Equal("trul", res.First().Name);
        }

        [Fact]
        public async Task GetExtensionShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleExtension.json");

            //Act
            var res = await _client.GetExtensionAsync("B");

            //Assert
            Assert.Equal("trul", res.Name);
            Assert.Equal(2, res.FieldTypes.Count);
            Assert.Collection(res.FieldTypes,
               (t) => Assert.Equal("Symbol", t),
               (t) => Assert.Equal("Text", t));
            Assert.Equal("https://robertlinde.se", res.Src);
        }

        [Fact]
        public async Task CreateExtensionShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleExtension.json");
            var ext = new UiExtension()
            {
                Name = "trul"
            };

            //Act
            var res = await _client.CreateExtensionAsync(ext);

            //Assert
            Assert.Equal("trul", res.Name);
            Assert.Equal(2, res.FieldTypes.Count);
            Assert.Collection(res.FieldTypes,
               (t) => Assert.Equal("Symbol", t),
               (t) => Assert.Equal("Text", t));
            Assert.Equal("https://robertlinde.se", res.Src);
        }

        [Fact]
        public async Task CreateOrUpdateExtensionShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleExtension.json");
            var ext = new UiExtension()
            {
                SystemProperties = new SystemProperties
                {
                    Id = "bob"
                },
                Name = "trul"
            };

            //Act
            var res = await _client.CreateOrUpdateExtensionAsync(ext);

            //Assert
            Assert.Equal("trul", res.Name);
            Assert.Equal(2, res.FieldTypes.Count);
            Assert.Collection(res.FieldTypes,
               (t) => Assert.Equal("Symbol", t),
               (t) => Assert.Equal("Text", t));
            Assert.Equal("https://robertlinde.se", res.Src);
        }

        [Fact]
        public async Task CreateOrUpdateExtensionShouldThrowIfNoIdSet()
        {
            //Arrange
            var ext = new UiExtension()
            {
                Name = "trul"
            };
            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.CreateOrUpdateExtensionAsync(ext));

            //Assert
            Assert.Equal($"The id of the extension must be set.{Environment.NewLine}Parameter name: extension", ex.Message);
        }

        [Theory]
        [InlineData("552")]
        [InlineData("wer324")]
        [InlineData("xn3315af")]
        public async Task DeletingExtensionShouldCallCorrectUrl(string id)
        {
            //Arrange
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.Response = new HttpResponseMessage();
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            await _client.DeleteExtensionAsync(id);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/extensions/{id}", requestUrl);
        }

        [Fact]
        public async Task CreatingATokenShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"CreatedToken.json");
            var token = new ManagementToken()
            {
                Name = "Brandao",
                Scopes = new List<string>
                {
                    "content_management_manage"
                }
            };

            //Act
            var res = await _client.CreateManagementTokenAsync(token);

            //Assert
            Assert.Equal("My Token", res.Name);
            Assert.Collection(res.Scopes, (c) => { Assert.Equal(SystemManagementScopes.Manage, c); });
            Assert.Equal("46d42a80f96db00393ffa867a753de126e658484dd8a20d209bcb7efcf3761b9", res.Token);
        }

        [Fact]
        public async Task GettingATokenShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"GetToken.json");

            //Act
            var res = await _client.GetManagementTokenAsync("pop");

            //Assert
            Assert.Equal("My Token", res.Name);
            Assert.Collection(res.Scopes, (c) => { Assert.Equal(SystemManagementScopes.Read, c); });
            Assert.Null(res.Token);
            Assert.Null(res.RevokedAt);
        }

        [Fact]
        public async Task GetTokensShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"TokensCollection.json");

            //Act
            var res = await _client.GetAllManagementTokensAsync();

            //Assert
            
            Assert.Collection(res, (c) => {
                Assert.Equal(SystemManagementScopes.Manage, c.Scopes.First());
                Assert.Equal(1, c.Scopes.Count);
                Assert.Null(c.Token);
                Assert.Equal("My Token", c.Name);
            });
        }

        [Fact]
        public async Task RevokingATokenShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"RevokedToken.json");

            //Act
            var res = await _client.RevokeManagementTokenAsync("pop");

            //Assert
            Assert.Equal("My Token", res.Name);
            Assert.Collection(res.Scopes, (c) => { Assert.Equal(SystemManagementScopes.Manage, c); });
            Assert.Null(res.Token);
            Assert.NotNull(res.RevokedAt);
        }

        [Fact]
        public async Task GettingTheCurrentUserShouldReturnCorrectResult()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"SampleUser.json");
            //Act
            var res = await _client.GetCurrentUserAsync();

            //Assert
            Assert.Equal("https://images.contentful.com/abcd1234", res.AvatarUrl);
        }

        [Fact]
        public async Task GettingOrganizationShouldReturnCorrectResult()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"OrganizationsCollection.json");
            //Act
            var res = await _client.GetOrganizationsAsync();

            //Assert
            Assert.Collection(res,
                (s) => { Assert.Equal("Silvertip", s.Name); },
                (s) => { Assert.Equal("CompanyBob", s.Name); },
                (s) => { Assert.Equal("CompanyAlice", s.Name); }
                );
        }

        [Fact]
        public async Task SerializedObjectShouldBeProperlyCapitalized()
        {
            //Arrange
            var fields = new CamelTest();
            fields.NotCamel = "Not a camel";
            fields.NotACamelEither = "Neither is this!";
            fields.LongThing = "This is though, a pure camel!";

            _handler.Response = GetResponseFromFile(@"SampleEntryManagement.json");
            var entry = new Entry<dynamic>();
            entry.Fields = fields;
            var contentSet = "";
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                 contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateEntryAsync(entry, "bluemoon");

            //Assert
            Assert.Contains("NotCamelISay", contentSet);
            Assert.Contains("NoCamelHere", contentSet);
            Assert.Contains("long", contentSet);
        }
    }
}
