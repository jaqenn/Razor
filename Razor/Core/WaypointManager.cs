﻿#region license

// Razor: An Ultima Online Assistant
// Copyright (C) 2020 Razor Development Community on GitHub <https://github.com/markdwags/Razor>
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Windows.Forms;
using System.Xml;
using Assistant.UI;

namespace Assistant.Core
{
    public static class WaypointManager
    {
        private static ListBox _waypointList;
        private static WaypointTimer _waypointTimer;

        public static Waypoint CurrentWaypoint;
        private static int _curWaypointIndex;

        private static List<Waypoint> _waypoints = new List<Waypoint>();

        public static void Initialize()
        {
            HotKey.Add(HKCategory.Misc, LocString.NextWaypoint, new HotKeyCallback(NextWaypoint));
            HotKey.Add(HKCategory.Misc, LocString.PrevWaypoint, new HotKeyCallback(PrevWaypoint));
            HotKey.Add(HKCategory.Misc, LocString.HideWaypoint, new HotKeyCallback(HideWaypoint));
        }

        public static void SetControls(ListBox waypointList)
        {
            _waypointList = waypointList;
            _waypointTimer = new WaypointTimer();
            _waypointTimer.Stop();
        }

        public class Waypoint
        {
            public int X { get; set; }
            public int Y { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                return $"{Name} ({X}, {Y})";
            }
        }

        public static void NextWaypoint()
        {
            _curWaypointIndex++;

            if (_curWaypointIndex >= _waypoints.Count)
                _curWaypointIndex = 0;

            ShowWaypoint(_waypoints[_curWaypointIndex]);
        }

        public static void PrevWaypoint()
        {
            _curWaypointIndex--;

            if (_curWaypointIndex < 0)
                _curWaypointIndex = _waypoints.Count - 1;

            ShowWaypoint(_waypoints[_curWaypointIndex]);
        }

        public static void AddWaypoint(Waypoint waypoint)
        {
            _waypoints.Add(waypoint);

            RedrawList();
        }

        public static void RemoveWaypoint(Waypoint waypoint)
        {
            _waypoints.Remove(waypoint);

            RedrawList();
        }

        public static void ShowWaypoint(Waypoint waypoint)
        {
            Client.Instance.SendToClient(new QuestArrow(true, Convert.ToUInt16(waypoint.X), Convert.ToUInt16(waypoint.Y)));
            
            CurrentWaypoint = waypoint;

            SendMessage($"Waypoint: {CurrentWaypoint}");

            if (Config.GetBool("ShowWaypointDistance"))
            {
                _waypointTimer.Start();
            }
        }

        public static void HideWaypoint()
        {
            Client.Instance.SendToClient(new QuestArrow(false, 0, 0));

            CurrentWaypoint = null;

            _waypointTimer.Stop();
        }

        public static void ResetTimer()
        {
            if (_waypointTimer.Running)
            {
                _waypointTimer.Stop();
                _waypointTimer = new WaypointTimer();
                _waypointTimer.Start();
            }
            else
            {
                _waypointTimer = new WaypointTimer();
                _waypointTimer.Stop();
            }
        }

        public static void StartTimer()
        {
            _waypointTimer.Start();
        }

        public static void StopTimer()
        {
            _waypointTimer.Stop();
        }

        private static void SendMessage(string msg)
        {
            if (Config.GetBool("ShowWaypointOverhead"))
            {
                World.Player.OverheadMessage(msg);
            }
            else
            {
                World.Player.SendMessage(MsgLevel.Info, msg);
            }
        }

        private class WaypointTimer : Timer
        {
            public WaypointTimer() : base(TimeSpan.FromSeconds(Config.GetInt("ShowWaypointSeconds")),
                TimeSpan.FromSeconds(Config.GetInt("ShowWaypointSeconds")))
            {
            }

            protected override void OnTick()
            {
                if (World.Player == null || CurrentWaypoint == null)
                {
                    Stop();
                    return;
                }

                int dist = Utility.Distance(World.Player.Position.X, World.Player.Position.Y, CurrentWaypoint.X,
                    CurrentWaypoint.Y);

                if (Config.GetBool("HideWaypoint") && dist <= Config.GetInt("HideWaypointDistance"))
                {
                    SendMessage($"Arrived at '{CurrentWaypoint}'");

                    Stop();
                    HideWaypoint();
                }
                else
                {
                    SendMessage($"{CurrentWaypoint.Name}: {dist} tiles away");
                }
            }
        }

        public static void Save(XmlTextWriter xml)
        {
            foreach (var waypoint in _waypoints)
            {
                xml.WriteStartElement("waypoint");
                xml.WriteAttributeString("x", waypoint.X.ToString());
                xml.WriteAttributeString("y", waypoint.Y.ToString());
                xml.WriteAttributeString("name", waypoint.Name);
                xml.WriteEndElement();
            }
        }

        public static void Load(XmlElement node)
        {
            ClearAll();

            try
            {
                foreach (XmlElement el in node.GetElementsByTagName("waypoint"))
                {
                    Waypoint waypoint = new Waypoint
                    {
                        X = Convert.ToInt32(el.GetAttribute("x")),
                        Y = Convert.ToInt32(el.GetAttribute("y")),
                        Name = el.GetAttribute("name")
                    };

                    _waypoints.Add(waypoint);
                }

                RedrawList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public static void ClearAll()
        {
            _waypoints.Clear();
        }

        public static void RedrawList()
        {
            _waypointList?.SafeAction(s =>
            {
                s.BeginUpdate();
                s.Items.Clear();

                foreach (Waypoint waypoint in _waypoints)
                {
                    s.Items.Add(waypoint);
                }

                s.EndUpdate();
            });
        }
    }
}