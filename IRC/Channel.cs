﻿#region License
// Copyright 2013 Tama Waddell <tamrix@gmail.com>
// 
// This file is part of SharpIRC.
// 
// SharpIRC is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// SharpIRC is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with SharpIRC.  If not, see <http://www.gnu.org/licenses/>.
// 
#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace IRC
{

    public sealed class Topic : EventArgs
    {
        public string Text { get; set; }
        public User ChangeUser { get; set; }

        public Topic()
        {
            Text = string.Empty;
            ChangeUser = null;
        }

        public Topic(string topicText)
        {
            Text = topicText;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    public sealed class Channel
    {
        public readonly ObservableCollection<User> Users = new ObservableCollection<User>();
        public string Name { get; private set; } // case insensitive
        public string Key { get; private set; } // set changes if OP
        public bool IsConnected { get; set; }
        // todo what permission you have on this channel
        public event EventHandler Joined;
        public event EventHandler Parted;
        public event EventHandler<Topic> TopicChanged; //todo domain type Topic? includes user who changed it?
        public event EventHandler<Message> Message;

        public Topic Topic {
            get { return _topic; } 
            set { _topic = value;  } // todo test admin then set topic?!?
        }

        private void OnMessage(Message e)
        {
            EventHandler<Message> handler = Message;
            if (handler != null) handler(this, e);
        }

        private void OnJoined()
        {
            if (Joined != null) Joined(this, EventArgs.Empty);
        }

        private void OnParted()
        {
            EventHandler handler = Parted;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnTopicChanged()
        {
            EventHandler<Topic> handler = TopicChanged;
            if (handler != null) handler(this, Topic);
        }

        public Channel(Client client, string name, string key = "")
        {
            Name = name;
            Key = key;
            _client = client;
        }

        public void Join()
        {
            if (_client.IsConnected)
            {
                _client.ReceivedReply += ProcessReply;
                _client.Join(Name, Key);
            }
        }

        public void Leave(string message = "")
        {
            _client.Part(Name, message);
        }

        public void Kick(User user)
        {

        }

        public void Invite(User user)
        {
            _client.Invite(user.Nick, Name);
        }

        public void Say(string message)
        {
            //_client.
        }

        public void ProcessReply(object sender, Reply reply)
        {

            // todo maybe check parm[0] for "name" before continuing
            // parm 0 is always channel name on text and nickname on code?!?
            // not sure why we bother to test this!?!
            //if (reply.Params[0] != _client.Nickname)
                //return;

            // todo maybe handle text only in client and delgate codes to the domain classes
            switch (reply.Command)
            {
                case "JOIN" :
                    if (reply.Params.Count <= 0 || reply.Params[0] != Name)
                        return;
                    // todo handle new users joining
                    OnJoined();
                    break;
                case "PRIVMSG" :
                    {
                        if (reply.Params.Count == 0 || reply.Params[0] != Name)
                            return;
                        User user = Users.First(x => x.Nick == reply.Prefix.Substring(0, reply.Prefix.IndexOf('!')));
                        OnMessage(new Message(user, reply.Trailing));
                        break;
                    }
                case "QUIT" :
                    {
                        string nick = reply.Prefix.Substring(0, reply.Prefix.IndexOf('!'));
                        User user = Users.FirstOrDefault(x => x.Nick == nick);
                        if (user == null)
                            return;
                        Users.Remove(user);
                        break;
                    }
            }

            int code;
            if (!int.TryParse(reply.Command, out code))
                return;

            var replyCode = (ReplyCode) code;
            switch (replyCode)
            {
                case ReplyCode.RplTopic :
                    if (reply.Params[1] != Name)
                        return;
                    Topic = new Topic(reply.Trailing);
                    OnTopicChanged();
                    break;
                case ReplyCode.RplTopicSetBy:
                    if (reply.Params[1] != Name)
                        return;
                            // 0 is client nickname
                    // todo may not use this
                    _client.Logger("Topic set by " + reply.Params[2]);
                    break;
                case ReplyCode.RplNameReply:
                    foreach (var user in reply.Trailing.Split().Select(name => new User(_client, name)).Where(user => !Users.Contains(user)))
                        Users.Add(user);
                    break;
                case ReplyCode.RplNoTopic:
                    Topic = new Topic();
                    break;
            }
        }

        private readonly Client _client;
        private Topic _topic;
    }

}