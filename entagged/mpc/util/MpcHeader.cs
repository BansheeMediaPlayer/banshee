// Copyright 2005 Rapha�l Slinckx <raphael@slinckx.net> 
//
// (see http://entagged.sourceforge.net)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See
// the License for the specific language governing permissions and
// limitations under the License.

/*
 * $Log$
 * Revision 1.1  2005/06/27 00:47:26  abock
 * Added entagged-sharp
 *
 * Revision 1.3  2005/02/08 12:54:40  kikidonk
 * Added cvs log and header
 *
 */

using System.Text;
using Entagged.Audioformats.Generic;

namespace Entagged.Audioformats.Mpc.Util {
	public class MpcHeader {
		
		byte[] b;
		public MpcHeader(byte[] b) {
			this.b = b;
		}
		
		public int SamplesNumber {
			get {
				if(b[0] == 7)
					return Utils.GetNumber(b, 1,4);

				return -1;
			}	
		}
		
		public int SamplingRate {
			get {
				if(b[0] == 7) {
					switch (b[6] & 0x02) {
						case 0: return 44100;
						case 1: return 48000;
		                case 2: return 37800;
		                case 3: return 32000;
		                default: return -1;
					}
				}
				
				return -1;
			}
		}
		
		public int ChannelNumber {
			get {
				if(b[0] == 7)
					return 2;
				
				return 2;
			}
		}
		
		public string EncodingType {
			get {
				StringBuilder sb = new StringBuilder().Append("MPEGplus (MPC)");
				if(b[0] == 7) {
					sb.Append(" rev.7, Profile:");
					switch ((b[7] & 0xF0) >> 4) {
						case 0: sb.Append( "No profile"); break;
						case 1: sb.Append( "Unstable/Experimental"); break;
						case 2: sb.Append( "Unused"); break;
						case 3: sb.Append( "Unused"); break;
						case 4: sb.Append( "Unused"); break;
						case 5: sb.Append( "Below Telephone (q= 0.0)"); break;
						case 6: sb.Append( "Below Telephone (q= 1.0)"); break;
						case 7: sb.Append( "Telephone (q= 2.0)"); break;
						case 8: sb.Append( "Thumb (q= 3.0)"); break;
						case 9: sb.Append( "Radio (q= 4.0)"); break;
						case 10: sb.Append( "Standard (q= 5.0)"); break;
						case 11: sb.Append( "Xtreme (q= 6.0)"); break;
						case 12: sb.Append( "Insane (q= 7.0)"); break;
						case 13: sb.Append( "BrainDead (q= 8.0)"); break;
						case 14: sb.Append( "Above BrainDead (q= 9.0)"); break;
						case 15: sb.Append( "Above BrainDead (q=10.0)"); break;
						default: sb.Append("No profile"); break;
					}
				}
				
				return sb.ToString();
			}
		}
		
		public string EncoderInfo {
			get {
				int encoder = b[24];
				StringBuilder sb = new StringBuilder().Append("Mpc encoder v").Append(((double)encoder)/100).Append(" ");
				if(encoder % 10 == 0)
					sb.Append("Release");
				else if(encoder %  2 == 0)
					sb.Append("Beta");
				else if(encoder %  2 == 1)
					sb.Append("Alpha");
				
				return sb.ToString();
			}
		}

	}
}
