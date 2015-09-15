﻿/*
 SDRSharp Net Remote

 http://eartoearoak.com/software/sdrsharp-net-remote

 Copyright 2014 - 2015 Al Brown

 A network remote control plugin for SDRSharp


 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, or (at your option)
 any later version.

 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using SDRSharp.Common;
using SDRSharp.Radio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace SDRSharp.NetRemote
{
    class Parser
    {
        private static string[] COMMANDS = { "get", "set", "exe" };
        private static Dictionary<string, Func<Client, bool, object, string>> METHODS =
                        new Dictionary<string, Func<Client, bool, object, string>>();

        private ISharpControl _control;
        private JavaScriptSerializer _json = new JavaScriptSerializer();

        public Parser(ISharpControl control)
        {

            _control = control;

            METHODS.Add("audiogain", CmdAudioGain);
            METHODS.Add("audioismuted", CmdAudioIsMuted);

            METHODS.Add("centrefrequency", CmdCentreFrequency);
            METHODS.Add("centerfrequency", CmdCentreFrequency);
            METHODS.Add("frequency", CmdFrequency);

            METHODS.Add("detectortype", CmdDetectorType);

            METHODS.Add("isplaying", CmdIsPlaying);

            METHODS.Add("sourceistunable", CmdSourceIsTunable);

            METHODS.Add("squelchenabled", CmdSquelchEnabled);
            METHODS.Add("squelchthreshold", CmdSquelchThreshold);

            METHODS.Add("fmstereo", CmdFmStereo);

            METHODS.Add("filtertype", CmdFilterType);
            METHODS.Add("filterbandwidth", CmdFilterBandwidth);
            METHODS.Add("filterorder", CmdFilterOrder);

            METHODS.Add("start", CmdAudioGain);
            METHODS.Add("stop", CmdAudioGain);
            METHODS.Add("close", CmdAudioGain);
        }

        public string Parse(Client client)
        {

            string result;
            string data = Regex.Replace(client.data.ToString(),
                                        @"[^\u0020-\u007F]", string.Empty);
            data = data.ToLower();

            try
            {
                Dictionary<string, object> requests =
                        (Dictionary<string, object>)_json.DeserializeObject(data);

                if (requests != null)
                {
                    object objCommand;
                    object objMethod;
                    object value;

                    requests.TryGetValue("command", out objCommand);
                    requests.TryGetValue("method", out objMethod);
                    requests.TryGetValue("value", out value);

                    if (!(objCommand is string))
                        throw new CommandException("Command should be a string");
                    if (!(objMethod is string))
                        throw new MethodException("Method should be a string");

                    string command = (string)objCommand;
                    string method = (string)objMethod;

                    if (command == null)
                        throw new CommandException("Command key not found");
                    if (Array.IndexOf(COMMANDS, command) == -1)
                        throw new CommandException(String.Format("Unknown command: {0}",
                            command));

                    if (method == null)
                        throw new MethodException("Method key not found");
                    if (!METHODS.ContainsKey(method))
                        throw new MethodException(String.Format("Unknown method: {0}",
                            method));

                    if (string.Equals(command, "set") && value == null)
                        throw new ValueException("Value missing");

                    result = Command(client, command, method, value);
                }
                else
                    result = null;
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is InvalidOperationException)
                    result = Error(client, "Syntax error", data);
                else if (ex is CommandException)
                    result = Error(client, "Command error", ex.Message);
                else if (ex is MethodException)
                    result = Error(client, "Method error", ex.Message);
                else if (ex is ValueException)
                    result = Error(client, "Value error", ex.Message);
                else if (ex is SourceException)
                    result = Error(client, "Source error", ex.Message);
                else
                    throw;
            }
            finally
            {
                client.data.Length = 0;
            }

            return result;
        }

        private string Command(Client client, string command, string method, object value)
        {
            string result;

            if (string.Equals(command, "exe"))
            {
                switch (method)
                {
                    case "start":
                        _control.StartRadio();
                        break;
                    case "stop":
                        _control.StopRadio();
                        break;
                    case "close":
                        throw new ClientException();
                    default:
                        throw new MethodException(String.Format("Unknown Exe method: {0}",
                                                  method));
                }
                result = Response<object>(client, null, null);
            }
            else
            {
                bool set = string.Equals(command, "set");
                result = METHODS[method].Invoke(client, set, value);
            }

            return result;
        }

        private object CheckValue<T>(object value)
        {
            Type typeExpected = typeof(T);
            Type typePassed = value.GetType();

            if (typeExpected == typeof(long))
                if (typePassed == typeof(long) || typePassed == typeof(int))
                    return value;

            if (typePassed != typeExpected)
            {
                if (typeExpected == typeof(bool))
                    throw new ValueException("Expected a boolean");
                if (typeExpected == typeof(int) || typeExpected == typeof(long))
                    throw new ValueException("Expected an integer");
                if (typeExpected == typeof(string))
                    throw new ValueException("Expected a string");
            }

            return value;
        }

        private void CheckRange(long value, long start, long end)
        {
            if (value < start)
                throw new ValueException(String.Format("Smaller than {0}", start));
            if (value > end)
                throw new ValueException(String.Format("Greater than {0}", end));
        }

        private object CheckEnum(string value, Type type)
        {
            return Enum.Parse(type, value, true);
        }

        private string CmdAudioGain(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                int gain = (int)CheckValue<int>(value);
                CheckRange(gain, 0, 40);
                _control.AudioGain = gain;
                result = Response<object>(client, null, null);
            }
            else
                result = Response<int>(client, "AudioGain",
                                _control.AudioGain);

            return result;
        }


        private string CmdAudioIsMuted(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                _control.AudioIsMuted = (bool)CheckValue<bool>(value);
                result = Response<object>(client, null, null);
            }
            else
                result = Response<bool>(client, "AudioIsMuted",
                                _control.AudioIsMuted);

            return result;
        }

        private string CmdCentreFrequency(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                if (!_control.SourceIsTunable)
                    throw new SourceException("Not tunable");
                long freq =
                    _json.ConvertToType<long>(CheckValue<long>(value));
                CheckRange(freq, 1, 999999999999);
                _control.CenterFrequency = freq;
                result = Response<object>(client, null, null);
            }
            else
                result = Response<long>(client, "CenterFrequency",
                                _control.CenterFrequency);

            return result;
        }

        private string CmdFrequency(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                if (!_control.SourceIsTunable)
                    throw new SourceException("Not tunable");
                long freq =
                    _json.ConvertToType<long>(CheckValue<long>(value));
                CheckRange(freq, 1, 999999999999);
                _control.Frequency = freq;
                result = Response<object>(client, null, null);
            }
            else
                result = Response<long>(client, "Frequency",
                                _control.Frequency);

            return result;
        }

        private string CmdDetectorType(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                string det = (string)(CheckValue<string>(value));
                _control.DetectorType =
                    (DetectorType)CheckEnum(det, typeof(DetectorType));
                result = Response<object>(client, null, null);
            }
            else
                result = Response<string>(client, "DetectorType",
                                 _control.DetectorType.ToString());

            return result;
        }

        private string CmdIsPlaying(Client client, bool set, object value)
        {
            string result;

            if (set)
                throw new MethodException("Read only");
            else
                result = Response<bool>(client, "IsPlaying",
                               _control.IsPlaying);

            return result;
        }

        private string CmdSourceIsTunable(Client client, bool set, object value)
        {
            string result;

            if (set)
                throw new MethodException("Read only");
            else
                result = Response<bool>(client, "SourceIsTunable",
                               _control.SourceIsTunable);

            return result;
        }

        private string CmdSquelchEnabled(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                _control.SquelchEnabled = (bool)CheckValue<bool>(value);
                result = Response<object>(client, null, null);
            }
            else
                result = Response<bool>(client, "SquelchEnabled",
                                _control.SquelchEnabled);

            return result;
        }

        private string CmdSquelchThreshold(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                int thresh = (int)CheckValue<int>(value);
                CheckRange(thresh, 0, 100);
                _control.SquelchThreshold = thresh;
                result = Response<object>(client, null, null);
            }
            else
                result = Response<int>(client, "SquelchThreshold",
                                _control.SquelchThreshold);

            return result;
        }

        private string CmdFmStereo(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                _control.FmStereo = (bool)CheckValue<bool>(value);
                result = Response<object>(client, null, null);
            }
            else
                result = Response<bool>(client, "FmStereo",
                                _control.FmStereo);

            return result;
        }

        private string CmdFilterType(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                int type = (int)CheckValue<int>(value);
                CheckRange(type, 0, Enum.GetNames(typeof(WindowType)).Length - 1);
                _control.FilterType = (WindowType)type;
                result = Response<object>(client, null, null);
            }
            else
                result = Response<int>(client, "FilterBandwidth",
                                _control.FilterType);

            return result;
        }

        private string CmdFilterBandwidth(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                int bw = (int)CheckValue<int>(value);
                CheckRange(bw, 0, 250000);
                _control.FilterBandwidth = bw;
                result = Response<object>(client, null, null);
            }
            else
                result = Response<int>(client, "FilterBandwidth",
                                _control.FilterBandwidth);

            return result;
        }

        private string CmdFilterOrder(Client client, bool set, object value)
        {
            string result;

            if (set)
            {
                int bw = (int)CheckValue<int>(value);
                CheckRange(bw, 0, 100);
                _control.FilterOrder = bw;
                result = Response<object>(client, null, null);
            }
            else
                result = Response<int>(client, "FilterOrder",
                                _control.FilterOrder);

            return result;
        }

        public string Motd(Client client)
        {
            Dictionary<string, string> version = new Dictionary<string, string>
            {
                {"Name", Info.Title()},
                {"Version", Info.Version()}
            };

            return _json.Serialize(version) + "\r\n";
        }

        private string Error(Client client, string type, string message)
        {
            Dictionary<string, string> version = new Dictionary<string, string>
            {
                {"Result", "Error"},
                {"Type", type},
                {"Message", message}
            };

            return  _json.Serialize(version) + "\r\n";
        }

        private string Response<T>(Client client, string key, object value)
        {
            Dictionary<string, object> resp = new Dictionary<string, object>
            {
                {"Result", "OK"}
            };

            if (key != null)
            {
                resp.Add("Method", key);
                resp.Add("Value", (T)value);
            }

            return _json.Serialize(resp) + "\r\n";
        }
    }

    class ClientException : Exception
    {
        public ClientException() : base() { }
    }

    class CommandException : Exception
    {
        public CommandException(string message) : base(message) { }
    }

    class MethodException : Exception
    {
        public MethodException(string message) : base(message) { }
    }

    class ValueException : Exception
    {
        public ValueException(string message) : base(message) { }
    }

    class SourceException : Exception
    {
        public SourceException(string message) : base(message) { }
    }
}
