﻿using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Energize.Services.MemoryStream;

namespace Energize.Services.Commands.Modules
{
    [CommandModule(Name="Administration")]
    class AdministrationCommands
    {

        private async Task Role(CommandContext ctx,string name,Action<SocketGuildUser> callback)
        {
            if (!(ctx.Message.Channel is IGuildChannel))
            {
                await ctx.EmbedReply.Danger(ctx.Message, name, "You can't do that in a DM channel");
                return;
            }

            SocketGuildUser user = ctx.Message.Author as SocketGuildUser;
            if (!user.GuildPermissions.Administrator) //&& user.Id != EnergizeConfig.OWNER_ID) debug
            {
                await ctx.EmbedReply.Danger(ctx.Message, name, "Only a server administrator can do that");
                return;
            }

            if (ctx.TryGetUser(ctx.Arguments[0],out SocketUser u))
            {
                if(u is SocketGuildUser)
                {
                    SocketGuildUser gu = u as SocketGuildUser;
                    callback(gu);
                }
                else
                {
                    await ctx.EmbedReply.Danger(ctx.Message, name, "This user is not part of the server!");
                }
            }
            else
            {
                await ctx.EmbedReply.Danger(ctx.Message, name, "No user was found for your input");
            }

        }

        [Command(Name="op",Help="Makes a user able to use administration commands",Usage="op <user>")]
        private async Task OP(CommandContext ctx)
        {
            await this.Role(ctx, "OP",async user =>
            {
                try
                {
                    IRole role = await ctx.GetOrCreateRole(user, "EnergizeAdmin");

                    await user.AddRoleAsync(role);
                    await ctx.EmbedReply.Good(ctx.Message, "OP", user.Username + "#" + user.Discriminator + " was succesfully allowed to use administration commands");
                }
                catch
                {
                    await ctx.EmbedReply.Danger(ctx.Message, "OP", "I don't have the rights to do that!");
                }
            });
        }

        [Command(Name="deop",Help="Disallow a user from using administration commands",Usage="deop <user>")]
        private async Task DeOP(CommandContext ctx)
        {
            await this.Role(ctx, "DeOP", async user =>
            {
                try
                {
                    IRole role = await ctx.GetOrCreateRole(user, "EnergizeAdmin");
                    if (role != null)
                    {
                        await user.RemoveRoleAsync(role);
                        await ctx.EmbedReply.Good(ctx.Message, "DeOP", user.Username + "#" + user.Discriminator + " was succesfully prevented from using administration commands");
                    }
                    else
                    {
                        await ctx.EmbedReply.Danger(ctx.Message, "DeOP", "You didn't op anybody yet!");
                    }
                }
                catch
                {
                    await ctx.EmbedReply.Danger(ctx.Message, "DeOP", "I don't have the rights to do that!");
                }
            });
        }

        private async Task ClearBase(CommandContext ctx,Action<IMessage,List<IMessage>> callback)
        {
            if (!ctx.IsAdminUser())
            {
                await ctx.EmbedReply.Danger(ctx.Message, "Clear", "You don't have the rights to do that");
                return;
            }

            int amount = 10;
            if (ctx.HasArguments)
            {
                if (int.TryParse(ctx.Arguments[0], out int a))
                {
                    if (a > 100)
                    {
                        a = 100;
                    }

                    if (a < 0)
                    {
                        a = 1;
                    }

                    amount = a;
                }
            }

            IEnumerable<IMessage> messages = ctx.Message.Channel.GetMessagesAsync().Flatten().ToEnumerable();
            List<IMessage> todelete = new List<IMessage>();
            int count = 0;
            foreach (IMessage msg in messages)
            {
                if(count < amount)
                {
                    callback(msg, todelete);
                    count++;
                }
            }

            try
            {
                ITextChannel chan = ctx.Message.Channel as ITextChannel;
                await chan.DeleteMessagesAsync(todelete);
                await ctx.EmbedReply.Good(ctx.Message, "Clear", "Cleared " + todelete.Count + " messages among " + amount);
            }
            catch
            {
                await ctx.EmbedReply.Danger(ctx.Message, "Clear", "I don't have the rights to do that!");
            }
        }

        [Command(Name="clear",Help="Clear the bot messages",Usage="clear <amounttoremove|nothing>")]
        private async Task Clear(CommandContext ctx)
        {
            ClientMemoryStream stream = ServiceManager.GetService("MemoryStream").Instance as ClientMemoryStream;
            ulong id = (await stream.GetClientInfo()).ID;
            await this.ClearBase(ctx, (msg,todelete) =>
            {
                bool old = (DateTime.Now.Date.Ticks - msg.CreatedAt.Date.Ticks > new DateTime().AddDays(15).Ticks);
                if (!old)
                {
                    SocketMessage mess = msg as SocketMessage;
                    if (msg.Author.Id == id)
                    {
                        todelete.Add(msg);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(msg.Content))
                        {
                            string cmd = ctx.Handler.GetCmd(msg.Content);
                            if (msg.Content.StartsWith(ctx.Prefix) && ctx.Handler.IsCmdLoaded(cmd))
                            {
                                todelete.Add(msg);
                            }
                        }
                    }
                }
            });
        }

        [Command(Name="clearbots",Help="Clear bot messages",Usage="clearbots <amounttoremove|nothing>")]
        private async Task ClearBots(CommandContext ctx)
        {
            await this.ClearBase(ctx, (msg, todelete) =>
            {
                bool old = (DateTime.Now.Date.Ticks - msg.CreatedAt.Date.Ticks > new DateTime().AddDays(15).Ticks);
                if (msg.Author.IsBot && !old)
                {
                    todelete.Add(msg);
                }
            });
        }

        [Command(Name="clearraw",Help="Clear every messages with the amount given",Usage="clearraw <amount>")]
        private async Task ClearRaw(CommandContext ctx)
        {
            await this.ClearBase(ctx, (msg, todelete) =>
            {
                todelete.Add(msg);
            });
        }

        private async Task<ITextChannel> GetOrCreateChannel(CommandContext ctx,string channame,string topic)
        {
            SocketGuildUser user = ctx.Message.Author as SocketGuildUser;
            SocketGuildChannel chan = user.Guild.Channels.Where(x => x != null && x.Name == channame).FirstOrDefault();
            ITextChannel c = null;
            if (chan == null)
            {
                RestTextChannel created = await user.Guild.CreateTextChannelAsync(channame);
                OverwritePermissions everyoneperms = new OverwritePermissions(
                    mentionEveryone: PermValue.Deny,
                    sendMessages: PermValue.Deny,
                    sendTTSMessages: PermValue.Deny
                    );
                OverwritePermissions botperm = new OverwritePermissions(
                    sendMessages: PermValue.Allow,
                    addReactions: PermValue.Allow
                    );
                await created.AddPermissionOverwriteAsync(user.Guild.EveryoneRole, everyoneperms);
                await created.AddPermissionOverwriteAsync(ctx.Client.CurrentUser, botperm);
                await created.ModifyAsync(prop =>
                {
                    prop.Topic = topic;
                });

                c = created;
            }
            else
            {
                c = chan as SocketTextChannel;
            }

            return c;
        }

        [Command(Name="shame",Help="Adds a message to the \"Hall of Shames\"\nYou can get message ids with discord developer mode",Usage="shame <messageid>")]
        private async Task Shame(CommandContext ctx)
        {
            if (!ctx.IsAdminUser())
            {
                await ctx.EmbedReply.Danger(ctx.Message, "Shame", "You don't have the rights to do that");
                return;
            }

            try
            {
                ITextChannel c = await this.GetOrCreateChannel(ctx, "hall_of_shames",
                    "Channel created by Energize, share unique and funny messages using " + ctx.Prefix + "shame");
                if(c == null)
                {
                    await ctx.EmbedReply.Danger(ctx.Message, "Shame", "I couldn't find or create \"Hall of Shames\" channel");
                    return;
                }

                if (!ctx.HasArguments)
                {
                    await ctx.EmbedReply.Danger(ctx.Message, "Shame", "You didn't provide any message id");
                    return;
                }

                if(ulong.TryParse(ctx.Arguments[0],out ulong mid))
                {
                    IMessage msg = await ctx.Message.Channel.GetMessageAsync(mid);
                    if (msg != null)
                    {
                        EmbedBuilder builder = new EmbedBuilder();
                        builder.WithAuthor(msg.Author);
                        builder.WithDescription(msg.Content);
                        builder.WithFooter("#" + msg.Channel.Name);
                        builder.WithTimestamp(msg.CreatedAt);
                        builder.WithColor(ctx.EmbedReply.ColorNormal);
                        string url = ctx.Handler.GetImageURLS(msg);
                        if(url != null)
                        {
                            builder.WithImageUrl(url);
                        }

                        IUserMessage posted = await ctx.EmbedReply.Send(c as SocketChannel, builder.Build());
                        if(posted != null)
                        {
                            await posted.AddReactionAsync(new Emoji("🌟"));
                            await ctx.EmbedReply.Good(ctx.Message, "Shame", "Message added to hall of shames");
                        }
                        else
                        {
                            await ctx.EmbedReply.Danger(ctx.Message, "Shame", "I wasn't able to post in \"Hall of Shames\" ?!");
                        }
                    }
                    else
                    {
                        await ctx.EmbedReply.Danger(ctx.Message, "Shame", "Sorry this message wasnt found or is not in the same channel");
                    }
                }
                else
                {
                    await ctx.EmbedReply.Danger(ctx.Message, "Shame", "You didn't input a message id");
                }

            }
            catch(Exception e)
            {
                EnergizeLog.Debug(e.ToString());
                await ctx.EmbedReply.Danger(ctx.Message, "Shame", "I don't have the rights to do that");
            }
        }

        [Command(Name="delinvites",
            Help="Deletes messages containing discord server invites\n"
            + "delete the \"EnergizeDeleteInvites\" role to cancel that feature",
            Usage="delinvites <nothing>")]
        private async Task DelInvites(CommandContext ctx)
        {
            if (!ctx.IsAdminUser())
            {
                await ctx.EmbedReply.Danger(ctx.Message, "DelInvites", "You don't have the rights to do that");
                return;
            }

            SocketGuildUser user = ctx.Message.Author as SocketGuildUser;
            IRole role = await ctx.GetOrCreateRole(user, "EnergizeDeleteInvites");

            if (role == null)
            {
                await ctx.EmbedReply.Danger(ctx.Message, "DelInvites", "I don't have the rights to do that");
                return;
            }

            try
            {
                IGuildUser bot = await ctx.RESTClient.GetGuildUserAsync(user.Guild.Id, ctx.Client.CurrentUser.Id);
                await bot.AddRoleAsync(role);

                await ctx.EmbedReply.Good(ctx.Message, "DelInvites", "I will now delete every messages containing an invite link, "
                    + "delete the \"EnergizeDeleteInvites\" role to remove that feature");
            }
            catch
            {
                await ctx.EmbedReply.Danger(ctx.Message, "DelInvites", "I don't have the rights to do that");
            }
        }
    }
}