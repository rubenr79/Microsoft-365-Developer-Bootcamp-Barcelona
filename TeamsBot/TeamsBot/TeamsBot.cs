// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema.Teams;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace TeamsBot
{
    public class EmptyBot : TeamsActivityHandler
    {
        protected override async Task<MessagingExtensionResponse> OnTeamsMessagingExtensionQueryAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionQuery query, CancellationToken cancellationToken)
        {
            var text = string.Empty;
            if (query.Parameters[0].Name.Equals("queryText"))
                text = query?.Parameters?[0]?.Value as string ?? string.Empty;


            var avengers = await FindAvengers(text);

            // We take every row of the results and wrap them in cards wrapped in in MessagingExtensionAttachment objects.
            // The Preview is optional, if it includes a Tap, that will trigger the OnTeamsMessagingExtensionSelectItemAsync event back on this bot.
            var attachments = avengers.Select(avenger =>
            {
                var previewCard = new ThumbnailCard { Title = avenger.Item1, Tap = new CardAction { Type = "invoke", Value = avenger } };
                if (!string.IsNullOrEmpty(avenger.Item5))
                {
                    previewCard.Images = new List<CardImage>() { new CardImage(avenger.Item4, "Icon") };
                }

                var attachment = new MessagingExtensionAttachment
                {
                    ContentType = HeroCard.ContentType,
                    Content = new HeroCard { Title = avenger.Item1 },
                    Preview = previewCard.ToAttachment()
                };

                return attachment;
            }).ToList();

            // The list of MessagingExtensionAttachments must we wrapped in a MessagingExtensionResult wrapped in a MessagingExtensionResponse.
            return new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = attachments
                }
            };
        }

        protected override Task<MessagingExtensionResponse> OnTeamsMessagingExtensionSelectItemAsync(ITurnContext<IInvokeActivity> turnContext, JObject query, CancellationToken cancellationToken)
        {
            // The Preview card's Tap should have a Value property assigned, this will be returned to the bot in this event. 
            var (name, actor, realname, image, link) = query.ToObject<(string, string, string, string, string)>();

            // We take every row of the results and wrap them in cards wrapped in in MessagingExtensionAttachment objects.
            // The Preview is optional, if it includes a Tap, that will trigger the OnTeamsMessagingExtensionSelectItemAsync event back on this bot.
            var card = new ThumbnailCard
            {
                Title = name,
                Subtitle = $"{actor}, {realname}",
                Buttons = new List<CardAction>
                    {
                        new CardAction { Type = ActionTypes.OpenUrl, Title = "Marvel Profile", Value = link },
                    },
            };

            if (!string.IsNullOrEmpty(image))
            {
                card.Images = new List<CardImage>() { new CardImage(image, "Icon") };
            }

            var attachment = new MessagingExtensionAttachment
            {
                ContentType = ThumbnailCard.ContentType,
                Content = card,
            };

            return Task.FromResult(new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = new List<MessagingExtensionAttachment> { attachment }
                }
            });
        }

        // Generate a set of substrings to illustrate the idea of a set of results coming back from a query. 
        private async Task<IEnumerable<(string, string, string, string, string)>> FindAvengers(string text)
        {
            try
            {
                var data = File.ReadAllText(@".\data\avengers.json");
                var obj = JObject.Parse(data);
                return obj["characters"]
                    .Where(item => item["name"].ToString().Contains(text, StringComparison.InvariantCultureIgnoreCase))
                    .Select(item => (item["name"].ToString(), item["actor"].ToString(), item["realname"].ToString(), item["image"]?.ToString(), item["link"].ToString()));
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        protected override async Task<MessagingExtensionActionResponse> OnTeamsMessagingExtensionSubmitActionAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionAction action, CancellationToken cancellationToken)
        {
            switch (action.CommandId)
            {
                // These commandIds are defined in the Teams App Manifest.
                case "CreateAvenger":
                    return CreateAvengerCommand(turnContext, action);

                default:
                    throw new NotImplementedException($"Invalid CommandId: {action.CommandId}");
            }
        }

        private MessagingExtensionActionResponse CreateAvengerCommand(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionAction action)
        {
            // The user has chosen to create a card by choosing the 'Create Card' context menu command.
            var createCardData = ((JObject)action.Data).ToObject<CreateCardData>();

            var card = new HeroCard
            {
                Title = createCardData.Name,
                Subtitle = createCardData.Actor,
                Images = new List<CardImage>
                {
                    new CardImage { Url = "https://res.cloudinary.com/teepublic/image/private/s--s0r6TuRK--/c_crop,x_10,y_10/c_fit,h_995/c_crop,g_north_west,h_1260,w_1008,x_-157,y_-192/co_rgb:0c3052,e_colorize,u_Misc:One%20Pixel%20Gray/c_scale,g_north_west,h_1260,w_1008/fl_layer_apply,g_north_west,x_-157,y_-192/bo_126px_solid_white/e_overlay,fl_layer_apply,h_1260,l_Misc:Art%20Print%20Bumpmap,w_1008/e_shadow,x_6,y_6/c_limit,h_1134,w_1134/c_lpad,g_center,h_1260,w_1260/b_rgb:eeeeee/c_limit,f_jpg,h_630,q_90,w_630/v1481201499/production/designs/923008_1.jpg" },
                }
            };

            var attachments = new List<MessagingExtensionAttachment>();
            attachments.Add(new MessagingExtensionAttachment
            {
                Content = card,
                ContentType = HeroCard.ContentType,
                Preview = card.ToAttachment(),
            });

            return new MessagingExtensionActionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    AttachmentLayout = "list",
                    Type = "result",
                    Attachments = attachments,
                },
            };
        }        
    }

    class CreateCardData
    {
        public string Name { get; set; }

        public string Actor { get; set; }

        public string Image { get; set; }
    }
}
