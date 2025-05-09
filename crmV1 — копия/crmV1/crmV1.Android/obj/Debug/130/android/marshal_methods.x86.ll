; ModuleID = 'obj\Debug\130\android\marshal_methods.x86.ll'
source_filename = "obj\Debug\130\android\marshal_methods.x86.ll"
target datalayout = "e-m:e-p:32:32-p270:32:32-p271:32:32-p272:64:64-f64:32:64-f80:32-n8:16:32-S128"
target triple = "i686-unknown-linux-android"


%struct.MonoImage = type opaque

%struct.MonoClass = type opaque

%struct.MarshalMethodsManagedClass = type {
	i32,; uint32_t token
	%struct.MonoClass*; MonoClass* klass
}

%struct.MarshalMethodName = type {
	i64,; uint64_t id
	i8*; char* name
}

%class._JNIEnv = type opaque

%class._jobject = type {
	i8; uint8_t b
}

%class._jclass = type {
	i8; uint8_t b
}

%class._jstring = type {
	i8; uint8_t b
}

%class._jthrowable = type {
	i8; uint8_t b
}

%class._jarray = type {
	i8; uint8_t b
}

%class._jobjectArray = type {
	i8; uint8_t b
}

%class._jbooleanArray = type {
	i8; uint8_t b
}

%class._jbyteArray = type {
	i8; uint8_t b
}

%class._jcharArray = type {
	i8; uint8_t b
}

%class._jshortArray = type {
	i8; uint8_t b
}

%class._jintArray = type {
	i8; uint8_t b
}

%class._jlongArray = type {
	i8; uint8_t b
}

%class._jfloatArray = type {
	i8; uint8_t b
}

%class._jdoubleArray = type {
	i8; uint8_t b
}

; assembly_image_cache
@assembly_image_cache = local_unnamed_addr global [0 x %struct.MonoImage*] zeroinitializer, align 4
; Each entry maps hash of an assembly name to an index into the `assembly_image_cache` array
@assembly_image_cache_hashes = local_unnamed_addr constant [226 x i32] [
	i32 32687329, ; 0: Xamarin.AndroidX.Lifecycle.Runtime => 0x1f2c4e1 => 65
	i32 34715100, ; 1: Xamarin.Google.Guava.ListenableFuture.dll => 0x211b5dc => 95
	i32 39109920, ; 2: Newtonsoft.Json.dll => 0x254c520 => 16
	i32 57263871, ; 3: Xamarin.Forms.Core.dll => 0x369c6ff => 90
	i32 101534019, ; 4: Xamarin.AndroidX.SlidingPaneLayout => 0x60d4943 => 79
	i32 120558881, ; 5: Xamarin.AndroidX.SlidingPaneLayout.dll => 0x72f9521 => 79
	i32 165246403, ; 6: Xamarin.AndroidX.Collection.dll => 0x9d975c3 => 46
	i32 182336117, ; 7: Xamarin.AndroidX.SwipeRefreshLayout.dll => 0xade3a75 => 80
	i32 209399409, ; 8: Xamarin.AndroidX.Browser.dll => 0xc7b2e71 => 44
	i32 230216969, ; 9: Xamarin.AndroidX.Legacy.Support.Core.Utils.dll => 0xdb8d509 => 60
	i32 232815796, ; 10: System.Web.Services => 0xde07cb4 => 104
	i32 261689757, ; 11: Xamarin.AndroidX.ConstraintLayout.dll => 0xf99119d => 49
	i32 278686392, ; 12: Xamarin.AndroidX.Lifecycle.LiveData.dll => 0x109c6ab8 => 64
	i32 280482487, ; 13: Xamarin.AndroidX.Interpolator => 0x10b7d2b7 => 58
	i32 318968648, ; 14: Xamarin.AndroidX.Activity.dll => 0x13031348 => 36
	i32 321597661, ; 15: System.Numerics => 0x132b30dd => 24
	i32 342366114, ; 16: Xamarin.AndroidX.Lifecycle.Common => 0x146817a2 => 62
	i32 385762202, ; 17: System.Memory.dll => 0x16fe439a => 23
	i32 441335492, ; 18: Xamarin.AndroidX.ConstraintLayout.Core => 0x1a4e3ec4 => 48
	i32 442521989, ; 19: Xamarin.Essentials => 0x1a605985 => 89
	i32 450948140, ; 20: Xamarin.AndroidX.Fragment.dll => 0x1ae0ec2c => 57
	i32 465846621, ; 21: mscorlib => 0x1bc4415d => 14
	i32 469710990, ; 22: System.dll => 0x1bff388e => 21
	i32 476646585, ; 23: Xamarin.AndroidX.Interpolator.dll => 0x1c690cb9 => 58
	i32 486930444, ; 24: Xamarin.AndroidX.LocalBroadcastManager.dll => 0x1d05f80c => 69
	i32 526420162, ; 25: System.Transactions.dll => 0x1f6088c2 => 98
	i32 548916678, ; 26: Microsoft.Bcl.AsyncInterfaces => 0x20b7cdc6 => 12
	i32 605376203, ; 27: System.IO.Compression.FileSystem => 0x24154ecb => 102
	i32 618636221, ; 28: K4os.Compression.LZ4.Streams => 0x24dfa3bd => 10
	i32 627609679, ; 29: Xamarin.AndroidX.CustomView => 0x2568904f => 53
	i32 662205335, ; 30: System.Text.Encodings.Web.dll => 0x27787397 => 32
	i32 663517072, ; 31: Xamarin.AndroidX.VersionedParcelable => 0x278c7790 => 85
	i32 666292255, ; 32: Xamarin.AndroidX.Arch.Core.Common.dll => 0x27b6d01f => 41
	i32 690569205, ; 33: System.Xml.Linq.dll => 0x29293ff5 => 35
	i32 722857257, ; 34: System.Runtime.Loader.dll => 0x2b15ed29 => 28
	i32 775507847, ; 35: System.IO.Compression => 0x2e394f87 => 101
	i32 809851609, ; 36: System.Drawing.Common.dll => 0x30455ad9 => 100
	i32 843511501, ; 37: Xamarin.AndroidX.Print => 0x3246f6cd => 76
	i32 928116545, ; 38: Xamarin.Google.Guava.ListenableFuture => 0x3751ef41 => 95
	i32 955402788, ; 39: Newtonsoft.Json => 0x38f24a24 => 16
	i32 967690846, ; 40: Xamarin.AndroidX.Lifecycle.Common.dll => 0x39adca5e => 62
	i32 974778368, ; 41: FormsViewGroup.dll => 0x3a19f000 => 6
	i32 983077409, ; 42: MySql.Data.dll => 0x3a989221 => 15
	i32 1012816738, ; 43: Xamarin.AndroidX.SavedState.dll => 0x3c5e5b62 => 78
	i32 1035644815, ; 44: Xamarin.AndroidX.AppCompat => 0x3dbaaf8f => 40
	i32 1042160112, ; 45: Xamarin.Forms.Platform.dll => 0x3e1e19f0 => 92
	i32 1052210849, ; 46: Xamarin.AndroidX.Lifecycle.ViewModel.dll => 0x3eb776a1 => 66
	i32 1098259244, ; 47: System => 0x41761b2c => 21
	i32 1175144683, ; 48: Xamarin.AndroidX.VectorDrawable.Animated => 0x460b48eb => 83
	i32 1178241025, ; 49: Xamarin.AndroidX.Navigation.Runtime.dll => 0x463a8801 => 73
	i32 1204270330, ; 50: Xamarin.AndroidX.Arch.Core.Common => 0x47c7b4fa => 41
	i32 1267360935, ; 51: Xamarin.AndroidX.VectorDrawable => 0x4b8a64a7 => 84
	i32 1293217323, ; 52: Xamarin.AndroidX.DrawerLayout.dll => 0x4d14ee2b => 55
	i32 1364015309, ; 53: System.IO => 0x514d38cd => 112
	i32 1365406463, ; 54: System.ServiceModel.Internals.dll => 0x516272ff => 105
	i32 1376866003, ; 55: Xamarin.AndroidX.SavedState => 0x52114ed3 => 78
	i32 1395857551, ; 56: Xamarin.AndroidX.Media.dll => 0x5333188f => 70
	i32 1406073936, ; 57: Xamarin.AndroidX.CoordinatorLayout => 0x53cefc50 => 50
	i32 1411638395, ; 58: System.Runtime.CompilerServices.Unsafe => 0x5423e47b => 26
	i32 1460219004, ; 59: Xamarin.Forms.Xaml => 0x57092c7c => 93
	i32 1462112819, ; 60: System.IO.Compression.dll => 0x57261233 => 101
	i32 1469204771, ; 61: Xamarin.AndroidX.AppCompat.AppCompatResources => 0x57924923 => 39
	i32 1487250139, ; 62: K4os.Hash.xxHash => 0x58a5a2db => 11
	i32 1520589185, ; 63: crmV1.Android.dll => 0x5aa25981 => 0
	i32 1582372066, ; 64: Xamarin.AndroidX.DocumentFile.dll => 0x5e5114e2 => 54
	i32 1592978981, ; 65: System.Runtime.Serialization.dll => 0x5ef2ee25 => 3
	i32 1622152042, ; 66: Xamarin.AndroidX.Loader.dll => 0x60b0136a => 68
	i32 1624863272, ; 67: Xamarin.AndroidX.ViewPager2 => 0x60d97228 => 87
	i32 1636350590, ; 68: Xamarin.AndroidX.CursorAdapter => 0x6188ba7e => 52
	i32 1639515021, ; 69: System.Net.Http.dll => 0x61b9038d => 2
	i32 1657153582, ; 70: System.Runtime => 0x62c6282e => 27
	i32 1658241508, ; 71: Xamarin.AndroidX.Tracing.Tracing.dll => 0x62d6c1e4 => 81
	i32 1658251792, ; 72: Xamarin.Google.Android.Material.dll => 0x62d6ea10 => 94
	i32 1670060433, ; 73: Xamarin.AndroidX.ConstraintLayout => 0x638b1991 => 49
	i32 1726116996, ; 74: System.Reflection.dll => 0x66e27484 => 111
	i32 1729485958, ; 75: Xamarin.AndroidX.CardView.dll => 0x6715dc86 => 45
	i32 1746115085, ; 76: System.IO.Pipelines.dll => 0x68139a0d => 22
	i32 1766324549, ; 77: Xamarin.AndroidX.SwipeRefreshLayout => 0x6947f945 => 80
	i32 1776026572, ; 78: System.Core.dll => 0x69dc03cc => 19
	i32 1788241197, ; 79: Xamarin.AndroidX.Fragment => 0x6a96652d => 57
	i32 1796167890, ; 80: Microsoft.Bcl.AsyncInterfaces.dll => 0x6b0f58d2 => 12
	i32 1808609942, ; 81: Xamarin.AndroidX.Loader => 0x6bcd3296 => 68
	i32 1813201214, ; 82: Xamarin.Google.Android.Material => 0x6c13413e => 94
	i32 1818569960, ; 83: Xamarin.AndroidX.Navigation.UI.dll => 0x6c652ce8 => 74
	i32 1867746548, ; 84: Xamarin.Essentials.dll => 0x6f538cf4 => 89
	i32 1878053835, ; 85: Xamarin.Forms.Xaml.dll => 0x6ff0d3cb => 93
	i32 1885316902, ; 86: Xamarin.AndroidX.Arch.Core.Runtime.dll => 0x705fa726 => 42
	i32 1919157823, ; 87: Xamarin.AndroidX.MultiDex.dll => 0x7264063f => 71
	i32 1925302748, ; 88: K4os.Compression.LZ4.dll => 0x72c1c9dc => 9
	i32 2011961780, ; 89: System.Buffers.dll => 0x77ec19b4 => 17
	i32 2019465201, ; 90: Xamarin.AndroidX.Lifecycle.ViewModel => 0x785e97f1 => 66
	i32 2055257422, ; 91: Xamarin.AndroidX.Lifecycle.LiveData.Core.dll => 0x7a80bd4e => 63
	i32 2079903147, ; 92: System.Runtime.dll => 0x7bf8cdab => 27
	i32 2090596640, ; 93: System.Numerics.Vectors => 0x7c9bf920 => 25
	i32 2097448633, ; 94: Xamarin.AndroidX.Legacy.Support.Core.UI => 0x7d0486b9 => 59
	i32 2126786730, ; 95: Xamarin.Forms.Platform.Android => 0x7ec430aa => 91
	i32 2201231467, ; 96: System.Net.Http => 0x8334206b => 2
	i32 2217644978, ; 97: Xamarin.AndroidX.VectorDrawable.Animated.dll => 0x842e93b2 => 83
	i32 2221085327, ; 98: crmV1.Android => 0x8463128f => 0
	i32 2244775296, ; 99: Xamarin.AndroidX.LocalBroadcastManager => 0x85cc8d80 => 69
	i32 2256548716, ; 100: Xamarin.AndroidX.MultiDex => 0x8680336c => 71
	i32 2261435625, ; 101: Xamarin.AndroidX.Legacy.Support.V4.dll => 0x86cac4e9 => 61
	i32 2265110946, ; 102: System.Security.AccessControl.dll => 0x8702d9a2 => 29
	i32 2279755925, ; 103: Xamarin.AndroidX.RecyclerView.dll => 0x87e25095 => 77
	i32 2315684594, ; 104: Xamarin.AndroidX.Annotation.dll => 0x8a068af2 => 37
	i32 2383496789, ; 105: System.Security.Principal.Windows.dll => 0x8e114655 => 31
	i32 2409053734, ; 106: Xamarin.AndroidX.Preference.dll => 0x8f973e26 => 75
	i32 2465532216, ; 107: Xamarin.AndroidX.ConstraintLayout.Core.dll => 0x92f50938 => 48
	i32 2471841756, ; 108: netstandard.dll => 0x93554fdc => 1
	i32 2475788418, ; 109: Java.Interop.dll => 0x93918882 => 8
	i32 2486824558, ; 110: K4os.Hash.xxHash.dll => 0x9439ee6e => 11
	i32 2498657740, ; 111: BouncyCastle.Cryptography.dll => 0x94ee7dcc => 4
	i32 2501346920, ; 112: System.Data.DataSetExtensions => 0x95178668 => 99
	i32 2505896520, ; 113: Xamarin.AndroidX.Lifecycle.Runtime.dll => 0x955cf248 => 65
	i32 2570120770, ; 114: System.Text.Encodings.Web => 0x9930ee42 => 32
	i32 2581819634, ; 115: Xamarin.AndroidX.VectorDrawable.dll => 0x99e370f2 => 84
	i32 2611359322, ; 116: ZstdSharp.dll => 0x9ba62e5a => 96
	i32 2620871830, ; 117: Xamarin.AndroidX.CursorAdapter.dll => 0x9c375496 => 52
	i32 2624644809, ; 118: Xamarin.AndroidX.DynamicAnimation => 0x9c70e6c9 => 56
	i32 2633051222, ; 119: Xamarin.AndroidX.Lifecycle.LiveData => 0x9cf12c56 => 64
	i32 2660759594, ; 120: System.Security.Cryptography.ProtectedData.dll => 0x9e97f82a => 107
	i32 2663698177, ; 121: System.Runtime.Loader => 0x9ec4cf01 => 28
	i32 2693849962, ; 122: System.IO.dll => 0xa090e36a => 112
	i32 2701096212, ; 123: Xamarin.AndroidX.Tracing.Tracing => 0xa0ff7514 => 81
	i32 2705697197, ; 124: crmV1 => 0xa145a9ad => 5
	i32 2732626843, ; 125: Xamarin.AndroidX.Activity => 0xa2e0939b => 36
	i32 2737747696, ; 126: Xamarin.AndroidX.AppCompat.AppCompatResources.dll => 0xa32eb6f0 => 39
	i32 2765824710, ; 127: System.Text.Encoding.CodePages.dll => 0xa4db22c6 => 106
	i32 2766581644, ; 128: Xamarin.Forms.Core => 0xa4e6af8c => 90
	i32 2778768386, ; 129: Xamarin.AndroidX.ViewPager.dll => 0xa5a0a402 => 86
	i32 2810250172, ; 130: Xamarin.AndroidX.CoordinatorLayout.dll => 0xa78103bc => 50
	i32 2819470561, ; 131: System.Xml.dll => 0xa80db4e1 => 34
	i32 2841355853, ; 132: System.Security.Permissions => 0xa95ba64d => 30
	i32 2853208004, ; 133: Xamarin.AndroidX.ViewPager => 0xaa107fc4 => 86
	i32 2855708567, ; 134: Xamarin.AndroidX.Transition => 0xaa36a797 => 82
	i32 2867946736, ; 135: System.Security.Cryptography.ProtectedData => 0xaaf164f0 => 107
	i32 2901442782, ; 136: System.Reflection => 0xacf080de => 111
	i32 2903344695, ; 137: System.ComponentModel.Composition => 0xad0d8637 => 103
	i32 2905242038, ; 138: mscorlib.dll => 0xad2a79b6 => 14
	i32 2916838712, ; 139: Xamarin.AndroidX.ViewPager2.dll => 0xaddb6d38 => 87
	i32 2919462931, ; 140: System.Numerics.Vectors.dll => 0xae037813 => 25
	i32 2921128767, ; 141: Xamarin.AndroidX.Annotation.Experimental.dll => 0xae1ce33f => 38
	i32 2944313911, ; 142: System.Configuration.ConfigurationManager.dll => 0xaf7eaa37 => 18
	i32 2968338931, ; 143: System.Security.Principal.Windows => 0xb0ed41f3 => 31
	i32 2978675010, ; 144: Xamarin.AndroidX.DrawerLayout => 0xb18af942 => 55
	i32 3012788804, ; 145: System.Configuration.ConfigurationManager => 0xb3938244 => 18
	i32 3024354802, ; 146: Xamarin.AndroidX.Legacy.Support.Core.Utils => 0xb443fdf2 => 60
	i32 3025069135, ; 147: K4os.Compression.LZ4.Streams.dll => 0xb44ee44f => 10
	i32 3044182254, ; 148: FormsViewGroup => 0xb57288ee => 6
	i32 3057625584, ; 149: Xamarin.AndroidX.Navigation.Common => 0xb63fa9f0 => 72
	i32 3089219899, ; 150: ZstdSharp => 0xb821c13b => 96
	i32 3111772706, ; 151: System.Runtime.Serialization => 0xb979e222 => 3
	i32 3124832203, ; 152: System.Threading.Tasks.Extensions => 0xba4127cb => 110
	i32 3132293585, ; 153: System.Security.AccessControl => 0xbab301d1 => 29
	i32 3204380047, ; 154: System.Data.dll => 0xbefef58f => 97
	i32 3211777861, ; 155: Xamarin.AndroidX.DocumentFile => 0xbf6fd745 => 54
	i32 3213246214, ; 156: System.Security.Permissions.dll => 0xbf863f06 => 30
	i32 3247949154, ; 157: Mono.Security => 0xc197c562 => 109
	i32 3258312781, ; 158: Xamarin.AndroidX.CardView => 0xc235e84d => 45
	i32 3265893370, ; 159: System.Threading.Tasks.Extensions.dll => 0xc2a993fa => 110
	i32 3267021929, ; 160: Xamarin.AndroidX.AsyncLayoutInflater => 0xc2bacc69 => 43
	i32 3317135071, ; 161: Xamarin.AndroidX.CustomView.dll => 0xc5b776df => 53
	i32 3317144872, ; 162: System.Data => 0xc5b79d28 => 97
	i32 3340431453, ; 163: Xamarin.AndroidX.Arch.Core.Runtime => 0xc71af05d => 42
	i32 3346324047, ; 164: Xamarin.AndroidX.Navigation.Runtime => 0xc774da4f => 73
	i32 3353484488, ; 165: Xamarin.AndroidX.Legacy.Support.Core.UI.dll => 0xc7e21cc8 => 59
	i32 3353544232, ; 166: Xamarin.CommunityToolkit.dll => 0xc7e30628 => 88
	i32 3358260929, ; 167: System.Text.Json => 0xc82afec1 => 33
	i32 3362522851, ; 168: Xamarin.AndroidX.Core => 0xc86c06e3 => 51
	i32 3366347497, ; 169: Java.Interop => 0xc8a662e9 => 8
	i32 3374999561, ; 170: Xamarin.AndroidX.RecyclerView => 0xc92a6809 => 77
	i32 3381033598, ; 171: K4os.Compression.LZ4 => 0xc9867a7e => 9
	i32 3395150330, ; 172: System.Runtime.CompilerServices.Unsafe.dll => 0xca5de1fa => 26
	i32 3404865022, ; 173: System.ServiceModel.Internals => 0xcaf21dfe => 105
	i32 3407215217, ; 174: Xamarin.CommunityToolkit => 0xcb15fa71 => 88
	i32 3429136800, ; 175: System.Xml => 0xcc6479a0 => 34
	i32 3430777524, ; 176: netstandard => 0xcc7d82b4 => 1
	i32 3441283291, ; 177: Xamarin.AndroidX.DynamicAnimation.dll => 0xcd1dd0db => 56
	i32 3467345667, ; 178: MySql.Data => 0xceab7f03 => 15
	i32 3476120550, ; 179: Mono.Android => 0xcf3163e6 => 13
	i32 3485117614, ; 180: System.Text.Json.dll => 0xcfbaacae => 33
	i32 3486566296, ; 181: System.Transactions => 0xcfd0c798 => 98
	i32 3493954962, ; 182: Xamarin.AndroidX.Concurrent.Futures.dll => 0xd0418592 => 47
	i32 3499097210, ; 183: Google.Protobuf.dll => 0xd08ffc7a => 7
	i32 3501239056, ; 184: Xamarin.AndroidX.AsyncLayoutInflater.dll => 0xd0b0ab10 => 43
	i32 3509114376, ; 185: System.Xml.Linq => 0xd128d608 => 35
	i32 3515174580, ; 186: System.Security.dll => 0xd1854eb4 => 108
	i32 3536029504, ; 187: Xamarin.Forms.Platform.Android.dll => 0xd2c38740 => 91
	i32 3567349600, ; 188: System.ComponentModel.Composition.dll => 0xd4a16f60 => 103
	i32 3605570793, ; 189: BouncyCastle.Cryptography => 0xd6e8a4e9 => 4
	i32 3618140916, ; 190: Xamarin.AndroidX.Preference => 0xd7a872f4 => 75
	i32 3627220390, ; 191: Xamarin.AndroidX.Print.dll => 0xd832fda6 => 76
	i32 3632359727, ; 192: Xamarin.Forms.Platform => 0xd881692f => 92
	i32 3633644679, ; 193: Xamarin.AndroidX.Annotation.Experimental => 0xd8950487 => 38
	i32 3641597786, ; 194: Xamarin.AndroidX.Lifecycle.LiveData.Core => 0xd90e5f5a => 63
	i32 3645630983, ; 195: Google.Protobuf => 0xd94bea07 => 7
	i32 3672681054, ; 196: Mono.Android.dll => 0xdae8aa5e => 13
	i32 3676310014, ; 197: System.Web.Services.dll => 0xdb2009fe => 104
	i32 3682565725, ; 198: Xamarin.AndroidX.Browser => 0xdb7f7e5d => 44
	i32 3684561358, ; 199: Xamarin.AndroidX.Concurrent.Futures => 0xdb9df1ce => 47
	i32 3689375977, ; 200: System.Drawing.Common => 0xdbe768e9 => 100
	i32 3703653692, ; 201: crmV1.dll => 0xdcc1453c => 5
	i32 3718780102, ; 202: Xamarin.AndroidX.Annotation => 0xdda814c6 => 37
	i32 3724971120, ; 203: Xamarin.AndroidX.Navigation.Common.dll => 0xde068c70 => 72
	i32 3748608112, ; 204: System.Diagnostics.DiagnosticSource => 0xdf6f3870 => 20
	i32 3758932259, ; 205: Xamarin.AndroidX.Legacy.Support.V4 => 0xe00cc123 => 61
	i32 3786282454, ; 206: Xamarin.AndroidX.Collection => 0xe1ae15d6 => 46
	i32 3822602673, ; 207: Xamarin.AndroidX.Media => 0xe3d849b1 => 70
	i32 3829621856, ; 208: System.Numerics.dll => 0xe4436460 => 24
	i32 3885922214, ; 209: Xamarin.AndroidX.Transition.dll => 0xe79e77a6 => 82
	i32 3896760992, ; 210: Xamarin.AndroidX.Core.dll => 0xe843daa0 => 51
	i32 3920810846, ; 211: System.IO.Compression.FileSystem.dll => 0xe9b2d35e => 102
	i32 3921031405, ; 212: Xamarin.AndroidX.VersionedParcelable.dll => 0xe9b630ed => 85
	i32 3931092270, ; 213: Xamarin.AndroidX.Navigation.UI => 0xea4fb52e => 74
	i32 3945713374, ; 214: System.Data.DataSetExtensions.dll => 0xeb2ecede => 99
	i32 3953953790, ; 215: System.Text.Encoding.CodePages => 0xebac8bfe => 106
	i32 3955647286, ; 216: Xamarin.AndroidX.AppCompat.dll => 0xebc66336 => 40
	i32 4023392905, ; 217: System.IO.Pipelines => 0xefd01a89 => 22
	i32 4025784931, ; 218: System.Memory => 0xeff49a63 => 23
	i32 4105002889, ; 219: Mono.Security.dll => 0xf4ad5f89 => 109
	i32 4151237749, ; 220: System.Core => 0xf76edc75 => 19
	i32 4182413190, ; 221: Xamarin.AndroidX.Lifecycle.ViewModelSavedState.dll => 0xf94a8f86 => 67
	i32 4185676441, ; 222: System.Security => 0xf97c5a99 => 108
	i32 4213026141, ; 223: System.Diagnostics.DiagnosticSource.dll => 0xfb1dad5d => 20
	i32 4260525087, ; 224: System.Buffers => 0xfdf2741f => 17
	i32 4292120959 ; 225: Xamarin.AndroidX.Lifecycle.ViewModelSavedState => 0xffd4917f => 67
], align 4
@assembly_image_cache_indices = local_unnamed_addr constant [226 x i32] [
	i32 65, i32 95, i32 16, i32 90, i32 79, i32 79, i32 46, i32 80, ; 0..7
	i32 44, i32 60, i32 104, i32 49, i32 64, i32 58, i32 36, i32 24, ; 8..15
	i32 62, i32 23, i32 48, i32 89, i32 57, i32 14, i32 21, i32 58, ; 16..23
	i32 69, i32 98, i32 12, i32 102, i32 10, i32 53, i32 32, i32 85, ; 24..31
	i32 41, i32 35, i32 28, i32 101, i32 100, i32 76, i32 95, i32 16, ; 32..39
	i32 62, i32 6, i32 15, i32 78, i32 40, i32 92, i32 66, i32 21, ; 40..47
	i32 83, i32 73, i32 41, i32 84, i32 55, i32 112, i32 105, i32 78, ; 48..55
	i32 70, i32 50, i32 26, i32 93, i32 101, i32 39, i32 11, i32 0, ; 56..63
	i32 54, i32 3, i32 68, i32 87, i32 52, i32 2, i32 27, i32 81, ; 64..71
	i32 94, i32 49, i32 111, i32 45, i32 22, i32 80, i32 19, i32 57, ; 72..79
	i32 12, i32 68, i32 94, i32 74, i32 89, i32 93, i32 42, i32 71, ; 80..87
	i32 9, i32 17, i32 66, i32 63, i32 27, i32 25, i32 59, i32 91, ; 88..95
	i32 2, i32 83, i32 0, i32 69, i32 71, i32 61, i32 29, i32 77, ; 96..103
	i32 37, i32 31, i32 75, i32 48, i32 1, i32 8, i32 11, i32 4, ; 104..111
	i32 99, i32 65, i32 32, i32 84, i32 96, i32 52, i32 56, i32 64, ; 112..119
	i32 107, i32 28, i32 112, i32 81, i32 5, i32 36, i32 39, i32 106, ; 120..127
	i32 90, i32 86, i32 50, i32 34, i32 30, i32 86, i32 82, i32 107, ; 128..135
	i32 111, i32 103, i32 14, i32 87, i32 25, i32 38, i32 18, i32 31, ; 136..143
	i32 55, i32 18, i32 60, i32 10, i32 6, i32 72, i32 96, i32 3, ; 144..151
	i32 110, i32 29, i32 97, i32 54, i32 30, i32 109, i32 45, i32 110, ; 152..159
	i32 43, i32 53, i32 97, i32 42, i32 73, i32 59, i32 88, i32 33, ; 160..167
	i32 51, i32 8, i32 77, i32 9, i32 26, i32 105, i32 88, i32 34, ; 168..175
	i32 1, i32 56, i32 15, i32 13, i32 33, i32 98, i32 47, i32 7, ; 176..183
	i32 43, i32 35, i32 108, i32 91, i32 103, i32 4, i32 75, i32 76, ; 184..191
	i32 92, i32 38, i32 63, i32 7, i32 13, i32 104, i32 44, i32 47, ; 192..199
	i32 100, i32 5, i32 37, i32 72, i32 20, i32 61, i32 46, i32 70, ; 200..207
	i32 24, i32 82, i32 51, i32 102, i32 85, i32 74, i32 99, i32 106, ; 208..215
	i32 40, i32 22, i32 23, i32 109, i32 19, i32 67, i32 108, i32 20, ; 216..223
	i32 17, i32 67 ; 224..225
], align 4

@marshal_methods_number_of_classes = local_unnamed_addr constant i32 0, align 4

; marshal_methods_class_cache
@marshal_methods_class_cache = global [0 x %struct.MarshalMethodsManagedClass] [
], align 4; end of 'marshal_methods_class_cache' array


@get_function_pointer = internal unnamed_addr global void (i32, i32, i32, i8**)* null, align 4

; Function attributes: "frame-pointer"="none" "min-legal-vector-width"="0" mustprogress nofree norecurse nosync "no-trapping-math"="true" nounwind sspstrong "stack-protector-buffer-size"="8" "stackrealign" "target-cpu"="i686" "target-features"="+cx8,+mmx,+sse,+sse2,+sse3,+ssse3,+x87" "tune-cpu"="generic" uwtable willreturn writeonly
define void @xamarin_app_init (void (i32, i32, i32, i8**)* %fn) local_unnamed_addr #0
{
	store void (i32, i32, i32, i8**)* %fn, void (i32, i32, i32, i8**)** @get_function_pointer, align 4
	ret void
}

; Names of classes in which marshal methods reside
@mm_class_names = local_unnamed_addr constant [0 x i8*] zeroinitializer, align 4
@__MarshalMethodName_name.0 = internal constant [1 x i8] c"\00", align 1

; mm_method_names
@mm_method_names = local_unnamed_addr constant [1 x %struct.MarshalMethodName] [
	; 0
	%struct.MarshalMethodName {
		i64 0, ; id 0x0; name: 
		i8* getelementptr inbounds ([1 x i8], [1 x i8]* @__MarshalMethodName_name.0, i32 0, i32 0); name
	}
], align 8; end of 'mm_method_names' array


attributes #0 = { "min-legal-vector-width"="0" mustprogress nofree norecurse nosync "no-trapping-math"="true" nounwind sspstrong "stack-protector-buffer-size"="8" uwtable willreturn writeonly "frame-pointer"="none" "target-cpu"="i686" "target-features"="+cx8,+mmx,+sse,+sse2,+sse3,+ssse3,+x87" "tune-cpu"="generic" "stackrealign" }
attributes #1 = { "min-legal-vector-width"="0" mustprogress "no-trapping-math"="true" nounwind sspstrong "stack-protector-buffer-size"="8" uwtable "frame-pointer"="none" "target-cpu"="i686" "target-features"="+cx8,+mmx,+sse,+sse2,+sse3,+ssse3,+x87" "tune-cpu"="generic" "stackrealign" }
attributes #2 = { nounwind }

!llvm.module.flags = !{!0, !1, !2}
!llvm.ident = !{!3}
!0 = !{i32 1, !"wchar_size", i32 4}
!1 = !{i32 7, !"PIC Level", i32 2}
!2 = !{i32 1, !"NumRegisterParameters", i32 0}
!3 = !{!"Xamarin.Android remotes/origin/d17-5 @ 45b0e144f73b2c8747d8b5ec8cbd3b55beca67f0"}
!llvm.linker.options = !{}
