﻿// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
namespace LibreLancer
{
	public static class LNetConst
	{
		public const int DEFAULT_PORT = 43443;
        public const uint PING_MAGIC = 0xBACAFEBA;
		public const string DEFAULT_APP_IDENT = "LIBRELANCER";
        public const string BROADCAST_KEY = "BROADCAST-LIBRELANCER";
        public const uint MAX_TICK_MS = 300000;
    }
}
