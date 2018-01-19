﻿using Discord;
using Discord.WebSocket;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;
using System.Text;

namespace Energize.Commands
{
    class Extras
    {
        private delegate string StyleCallback(string input);
        private static Dictionary<string,StyleCallback> StyleCallbacks = new Dictionary<string,StyleCallback>
        {
            ["owo"] = input => {
                string result = input.Replace("r","w")
                    .Replace("R","W")
                    .Replace("l","w")
                    .Replace("L","W");
                Random rand = new Random();
                if (rand.Next(0,100) > 50)
                {
                    result += "~";
                }

                if(rand.Next(0,100) < 25)
                {
                    if(rand.Next(0,100) > 50)
                    {
                        result += " owo";
                    }
                    else
                    {
                        result += " uwu";
                    }
                }

                return result;
            },
            ["crazy"] = input => {
                string content = input;
                string result = "";
                Random rand = new Random();
                foreach(char letter in content)
                {
                    string part = letter.ToString();
                    if(rand.Next(1,100) >= 50)
                    {
                        part = part.ToUpper();
                    }
                    else
                    {
                        part = part.ToLower();
                    }

                    result += part;
                }

                return result;
            },
            ["reverse"] = input => {
                char[] chars = input.ToCharArray();
                Array.Reverse(chars);

                return new string(chars);
            },
            ["anime"] = input => {
                input = input.First().ToString().ToUpper() + input.Substring(1);
                string result = "";
                foreach(char c in input)
                {
                    if(char.IsLetter(c))
                    {
                        if(char.IsUpper(c))
                        {
                            byte converted = (byte)(161 + Convert.ToByte(c) - 65);
                            string utf8 = Encoding.UTF8.GetString(new byte[]{ 239, 188, converted});

                            result += utf8;
                        }
                        else
                        {
                            byte converted = (byte)(130 + Convert.ToByte(c) - 98);
                            string utf8 = Encoding.UTF8.GetString(new byte[]{ 239, 189, converted});

                            result += utf8;
                        }
                    }
                    else
                    {
                        result += c;
                    }
                }
                result = result.Replace(".","．")
                    .Replace("!", "！")
                    .Replace(" ","\t");

                Random rand = new Random();
                if(rand.Next(0,100) > 90)
                {
                    result += "～";
                }

                if(rand.Next(0,100) > 75)
                {
                    result += "．．";
                }

                if(rand.Next(0,100) > 75)
                {
                    result += "！！";
                }

                string[] decoration = EnergizeData.ANIME_DECORATIONS[rand.Next(0,EnergizeData.ANIME_DECORATIONS.Length-1)];
                result = decoration[0] + result + decoration[1];

                string emote = EnergizeData.ANIME_EMOTES[rand.Next(0,EnergizeData.ANIME_EMOTES.Length-1)];
                if(rand.Next(0,100) > 50)
                {
                    result = emote + " － " + result;
                }
                else
                {
                    result = result + " － " + emote;
                }

                return result;
            },
            ["kid"] = input => {
                Random rand = new Random();
                string result = input.ToLower()
                    .Replace("i will","ima")
                    .Replace("i dont know","idk")
                    .Replace("dont know","dunno")
                    .Replace("because","cuz")
                    .Replace("seriously","srs")
                    .Replace("you are","you is")
                    .Replace("they are","they is")
                    .Replace("what","wat")
                    .Replace("you're","ur")
                    .Replace("you","u")
                    .Replace("people","ppl")
                    .Replace("that","taht")
                    .Replace("this","dis")
                    .Replace("please","pls")
                    .Replace("arent","aint")
                    .Replace("than","den")
                    .Replace("fucking","fuken")
                    .Replace("kid","kiddi")
                    .Replace("see","c")
                    .Replace("yes","ye")
                    .Replace("the","da")
                    .Replace("why","y")

                    .Replace("'","")
                    .Replace(".","!!1 ")
                    .Replace(","," ")
                    .Replace("to","2")
                    .Replace("for","4")
                    .Replace("oh","o")
                    .Replace("be","B")
                    .Replace("ll","l")
                    .Replace("nn","n")
                    .Replace("pp","p")
                    .Replace("ck","k");

                if(rand.Next(0,100) > 50)
                {
                    int rnum = rand.Next(0,100);
                    if(rnum > 66)
                    {
                        int amount = rand.Next(1,3);
                        result += " :" + new string('D',amount);
                    }
                    else if(rnum < 33)
                    {
                        int amount = rand.Next(1,3);
                        result += " X" + new string('D',amount);
                    }
                    else
                    {
                        result += " =)";
                    }
                }

                return result;
            },
            ["leet"] = input => {
                string result = input.ToLower()
                    .Replace("e","3")
                    .Replace("a","4")
                    .Replace("i","1")
                    .Replace("o","0");

                return result;
            }
        };

        public static async void InviteCheck(SocketMessage msg, CommandReplyEmbed embedrep)
        {
            if (msg.Channel is IDMChannel) return;
            SocketGuildChannel chan = msg.Channel as SocketGuildChannel;
            string pattern = @"discord\.gg\/.+\s?";
            if (Regex.IsMatch(msg.Content, pattern) && msg.Author.Id != EnergizeConfig.BOT_ID_MAIN)
            {
                if(chan.Guild.Roles.Any(x => x.Name == "EnergizeDeleteInvites"))
                {
                    try
                    {
                        EmbedBuilder builder = new EmbedBuilder();
                        embedrep.BuilderWithAuthor(msg,builder);
                        builder.WithDescription("Your message was removed, it contained an invitation link");
                        builder.WithFooter("Invite Checker");
                        builder.WithColor(embedrep.ColorWarning);

                        await msg.DeleteAsync();
                        await embedrep.Send(msg, builder.Build());
                    }
                    catch
                    {
                        await embedrep.Danger(msg, "Invite Checker", "I couldn't delete this message"
                            + " because I don't have the rights necessary for that");
                    }

                }
            }
        }

        public static string GetStyleResult(string input,string style)
        {
            if(StyleCallbacks.ContainsKey(style))
            {
                return StyleCallbacks[style](input);
            }
            else
            {
                return input;
            }
        }

        public static List<string> GetStyles()
        {
            List<string> styles = new List<string>();
            foreach(KeyValuePair<string,StyleCallback> callback in StyleCallbacks)
            {
                styles.Add(callback.Key);
            }

            return styles;
        }

        public static async void Stylify(SocketMessage msg,CommandReplyEmbed embedrep)
        {
            if(msg.Channel is IDMChannel || msg.Author.IsBot) return;
            SocketGuildUser user = msg.Author as SocketGuildUser;
            string identifier = "EnergizeStyle: ";

            IRole role = user.Roles.Where(x => x.Name.StartsWith(identifier)).FirstOrDefault();
            if(role != null)
            {
                string style = role.Name.Remove(0,identifier.Length);
                if(StyleCallbacks.ContainsKey(style))
                {
                    try
                    {
                        string result = GetStyleResult(msg.Content,style);
                        EmbedBuilder builder = new EmbedBuilder();
                        embedrep.BuilderWithAuthor(msg,builder);
                        builder.WithDescription(result);
                        builder.WithColor(embedrep.ColorNormal);

                        await msg.DeleteAsync();

                        if(result.Length > 2000)
                        {
                            await embedrep.Danger(msg,"Style","Message was over discord message limit");
                        }
                        else
                        {
                            await embedrep.Send(msg,builder.Build());
                        }
                    }
                    catch
                    {

                    }
                }
            }
        }

        public static async void Hentai(SocketMessage msg,CommandReplyEmbed embedrep)
        {
            if(msg.Channel is IDMChannel || msg.Author.IsBot) return;
            if(msg.Content.ToLower().Contains("hentai"))
            {
                IGuildChannel chan = msg.Channel as IGuildChannel;
                SocketGuild guild = chan.Guild as SocketGuild;
                IReadOnlyList<SocketUser> users = guild.Users as IReadOnlyList<SocketUser>;

                Random rand = new Random();
                string quote = EnergizeData.HENTAI_QUOTES[rand.Next(0,EnergizeData.HENTAI_QUOTES.Length-1)];
                quote = quote.Replace("{NAME}",msg.Author.Username);
                quote = GetStyleResult(quote,"anime");

                EmbedBuilder builder = new EmbedBuilder();
                builder.WithColor(new Color(255, 150, 255));
                builder.WithDescription(quote);
                builder.WithFooter("Hentai");

                await embedrep.Send(msg,builder.Build());
            }
        }
    }
}