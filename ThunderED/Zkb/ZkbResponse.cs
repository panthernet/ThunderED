// ***********************************************************************
// Assembly         : EveLib.ZKillboard
// Author           : Lars Kristian
// Created          : 06-18-2014
//
// Last Modified By : Lars Kristian
// Last Modified On : 06-19-2014
// ***********************************************************************
// <copyright file="ZkbResponse.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;

namespace ThunderED.Zkb {
       /// <summary>
    ///     Class ZkbResponse.
    /// </summary>
    [DataContract]
    public class ZkbResponse : Collection<ZkbResponse.ZkbKill> {
        /// <summary>
        ///     Gets or sets the request count.
        /// </summary>
        /// <value>The request count.</value>
        public int RequestCount { get; set; }

        /// <summary>
        ///     Gets or sets the maximum requests.
        /// </summary>
        /// <value>The maximum requests.</value>
        public int MaxRequests { get; set; }


        /// <summary>
        ///     Class ZkbAttacker.
        /// </summary>
        [DataContract]
        public class ZkbAttacker : ZkbEntity {
            /// <summary>
            ///     Gets or sets the damage done.
            /// </summary>
            /// <value>The damage done.</value>
            [DataMember(Name = "damage_done")]
            public int DamageDone { get; set; }

            /// <summary>
            ///     Gets or sets the security status.
            /// </summary>
            /// <value>The security status.</value>
            [DataMember(Name = "security_status")]
            public double SecurityStatus { get; set; }

            ///// <summary>
            /////     Sets the final blow string.
            ///// </summary>
            ///// <value>The final blow string.</value>
            //[DataMember(Name = "finalBlow")]
            //public string FinalBlowString {
            //    set { FinalBlow = value == "1"; }
            //}

            /// <summary>
            ///     Gets or sets a value indicating whether [final blow].
            /// </summary>
            /// <value><c>true</c> if [final blow]; otherwise, <c>false</c>.</value>
            [DataMember(Name = "final_blow")]
            //[JsonConverter(typeof(BoolConverter))]
            public bool FinalBlow { get; set; }

            /// <summary>
            ///     Gets or sets the weapon type identifier.
            /// </summary>
            /// <value>The weapon type identifier.</value>
            [DataMember(Name = "weapon_type_id")]
            public int WeaponTypeId { get; set; }


            ///// <summary>
            /////     Gets or sets the ship type identifier.
            ///// </summary>
            ///// <value>The ship type identifier.</value>
            //[DataMember(Name = "shipTypeID")]
            //public int ShipTypeId { get; set; }
        }


        /// <summary>
        ///     Class ZkbEntity.
        /// </summary>
        [DataContract]
        public abstract class ZkbEntity {
            /// <summary>
            ///     Gets or sets the ship type identifier.
            /// </summary>
            /// <value>The ship type identifier.</value>
            [DataMember(Name = "ship_type_id")]
            public int ShipTypeId { get; set; }

            /// <summary>
            ///     Gets or sets the character identifier.
            /// </summary>
            /// <value>The character identifier.</value>
            [DataMember(Name = "character_id")]
            public long CharacterId { get; set; }

			/// <summary>
			///     Gets or sets the corporation identifier.
			/// </summary>
			/// <value>The corporation identifier.</value>
			[DataMember(Name = "corporation_id")]
            public long CorporationId { get; set; }

            /// <summary>
            ///     Gets or sets the alliance identifier.
            /// </summary>
            /// <value>The alliance identifier.</value>
            [DataMember(Name = "alliance_id")]
            public int AllianceId { get; set; }

			/// <summary>
			///     Gets or sets the faction identifier.
			/// </summary>
			/// <value>The faction identifier.</value>
			[DataMember(Name = "faction_id")]
			public int FactionId { get; set; }
		}

        /// <summary>
        ///     Class ZkbItem.
        /// </summary>
        [DataContract]
        public class ZkbItem {
            /// <summary>
            ///     Gets or sets the type identifier.
            /// </summary>
            /// <value>The type identifier.</value>
            [DataMember(Name = "item_type_id")]
            public int TypeId { get; set; }

            /// <summary>
            ///     Gets or sets the flag.
            /// </summary>
            /// <value>The flag.</value>
            [DataMember(Name = "flag")]
            public int Flag { get; set; }

            /// <summary>
            ///     Gets or sets the quantity dropped.
            /// </summary>
            /// <value>The quantity dropped.</value>
            [DataMember(Name = "quantity_dropped")]
            public int QuantityDropped { get; set; }

            /// <summary>
            ///     Sets the singleton string.
            /// </summary>
            /// <value>The singleton string.</value>
            [DataMember(Name = "singleton")]
            public string SingletonString {
                set { Singleton = value == "1"; }
            }

            /// <summary>
            ///     Gets or sets a value indicating whether this <see cref="ZkbItem" /> is singleton.
            /// </summary>
            /// <value><c>true</c> if singleton; otherwise, <c>false</c>.</value>
            public bool Singleton { get; set; }
        }

        /// <summary>
        ///     Class ZkbKill.
        /// </summary>
        [DataContract]
        public class ZkbKill {
            /// <summary>
            ///     Gets or sets the kill identifier.
            /// </summary>
            /// <value>The kill identifier.</value>
            [DataMember(Name = "killmail_id")]
            public long KillId { get; set; }

            /// <summary>
            ///     Gets or sets the solar system identifier.
            /// </summary>
            /// <value>The solar system identifier.</value>
            [DataMember(Name = "solar_system_id")]
            public int SolarSystemId { get; set; }

            /// <summary>
            ///     Gets or sets the kill time.
            /// </summary>
            /// <value>The kill time.</value>
            [DataMember(Name = "killmail_time")]
            public DateTime KillTime { get; set; }

            /// <summary>
            ///     Gets or sets the victim.
            /// </summary>
            /// <value>The victim.</value>
            [DataMember(Name = "victim")]
            public ZkbVictim Victim { get; set; }

            /// <summary>
            ///     Gets or sets the attackers.
            /// </summary>
            /// <value>The attackers.</value>
            [DataMember(Name = "attackers")]
            public IList<ZkbAttacker> Attackers { get; set; }

            /// <summary>
            ///     Gets or sets the stats.
            /// </summary>
            /// <value>The stats.</value>
            [DataMember(Name = "zkb")]
            public ZkbStats Stats { get; set; }
        }

        /// <summary>
        ///     Class ZkbStats.
        /// </summary>
        [DataContract]
        public class ZkbStats {
            /// <summary>
            ///     Gets or sets the total value.
            /// </summary>
            /// <value>The total value.</value>
            [DataMember(Name = "totalValue")]
            public decimal TotalValue { get; set; }

            /// <summary>
            ///     Gets or sets the points.
            /// </summary>
            /// <value>The points.</value>
            [DataMember(Name = "points")]
            public int Points { get; set; }
        }

        /// <summary>
        ///     Class ZkbVictim.
        /// </summary>
        [DataContract]
        public class ZkbVictim : ZkbEntity {
            /// <summary>
            ///     Gets or sets the damage taken.
            /// </summary>
            /// <value>The damage taken.</value>
            [DataMember(Name = "damage_taken")]
            public int DamageTaken { get; set; }

			/// <summary>
			///     Gets or sets the items.
			/// </summary>
			/// <value>The items.</value>
			[DataMember(Name = "items")]
			public IList<ZkbItem> Items { get; set; }

		}
	}
}
