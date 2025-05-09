; ModuleID = 'obj\Debug\130\android\marshal_methods.arm64-v8a.ll'
source_filename = "obj\Debug\130\android\marshal_methods.arm64-v8a.ll"
target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
target triple = "aarch64-unknown-linux-android"


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
@assembly_image_cache = local_unnamed_addr global [0 x %struct.MonoImage*] zeroinitializer, align 8
; Each entry maps hash of an assembly name to an index into the `assembly_image_cache` array
@assembly_image_cache_hashes = local_unnamed_addr constant [226 x i64] [
	i64 24362543149721218, ; 0: Xamarin.AndroidX.DynamicAnimation => 0x568d9a9a43a682 => 56
	i64 120698629574877762, ; 1: Mono.Android => 0x1accec39cafe242 => 13
	i64 210515253464952879, ; 2: Xamarin.AndroidX.Collection.dll => 0x2ebe681f694702f => 46
	i64 232391251801502327, ; 3: Xamarin.AndroidX.SavedState.dll => 0x3399e9cbc897277 => 78
	i64 295915112840604065, ; 4: Xamarin.AndroidX.SlidingPaneLayout => 0x41b4d3a3088a9a1 => 79
	i64 486223428996552534, ; 5: ZstdSharp.dll => 0x6bf69a1eecfd756 => 96
	i64 595053104451889001, ; 6: MySql.Data => 0x8420da551592769 => 15
	i64 634308326490598313, ; 7: Xamarin.AndroidX.Lifecycle.Runtime.dll => 0x8cd840fee8b6ba9 => 65
	i64 702024105029695270, ; 8: System.Drawing.Common => 0x9be17343c0e7726 => 100
	i64 720058930071658100, ; 9: Xamarin.AndroidX.Legacy.Support.Core.UI => 0x9fe29c82844de74 => 59
	i64 872800313462103108, ; 10: Xamarin.AndroidX.DrawerLayout => 0xc1ccf42c3c21c44 => 55
	i64 940822596282819491, ; 11: System.Transactions => 0xd0e792aa81923a3 => 98
	i64 996343623809489702, ; 12: Xamarin.Forms.Platform => 0xdd3b93f3b63db26 => 92
	i64 1000557547492888992, ; 13: Mono.Security.dll => 0xde2b1c9cba651a0 => 109
	i64 1120440138749646132, ; 14: Xamarin.Google.Android.Material.dll => 0xf8c9a5eae431534 => 94
	i64 1315114680217950157, ; 15: Xamarin.AndroidX.Arch.Core.Common.dll => 0x124039d5794ad7cd => 41
	i64 1425944114962822056, ; 16: System.Runtime.Serialization.dll => 0x13c9f89e19eaf3a8 => 3
	i64 1624659445732251991, ; 17: Xamarin.AndroidX.AppCompat.AppCompatResources.dll => 0x168bf32877da9957 => 39
	i64 1628611045998245443, ; 18: Xamarin.AndroidX.Lifecycle.ViewModelSavedState.dll => 0x1699fd1e1a00b643 => 67
	i64 1636321030536304333, ; 19: Xamarin.AndroidX.Legacy.Support.Core.Utils.dll => 0x16b5614ec39e16cd => 60
	i64 1731380447121279447, ; 20: Newtonsoft.Json => 0x18071957e9b889d7 => 16
	i64 1743969030606105336, ; 21: System.Memory.dll => 0x1833d297e88f2af8 => 23
	i64 1769105627832031750, ; 22: Google.Protobuf => 0x188d203205129a06 => 7
	i64 1795316252682057001, ; 23: Xamarin.AndroidX.AppCompat.dll => 0x18ea3e9eac997529 => 40
	i64 1836611346387731153, ; 24: Xamarin.AndroidX.SavedState => 0x197cf449ebe482d1 => 78
	i64 1865037103900624886, ; 25: Microsoft.Bcl.AsyncInterfaces => 0x19e1f15d56eb87f6 => 12
	i64 1875917498431009007, ; 26: Xamarin.AndroidX.Annotation.dll => 0x1a08990699eb70ef => 37
	i64 1981742497975770890, ; 27: Xamarin.AndroidX.Lifecycle.ViewModel.dll => 0x1b80904d5c241f0a => 66
	i64 1986553961460820075, ; 28: Xamarin.CommunityToolkit => 0x1b91a84d8004686b => 88
	i64 2040001226662520565, ; 29: System.Threading.Tasks.Extensions.dll => 0x1c4f8a4ea894a6f5 => 110
	i64 2133195048986300728, ; 30: Newtonsoft.Json.dll => 0x1d9aa1984b735138 => 16
	i64 2136356949452311481, ; 31: Xamarin.AndroidX.MultiDex.dll => 0x1da5dd539d8acbb9 => 71
	i64 2165725771938924357, ; 32: Xamarin.AndroidX.Browser => 0x1e0e341d75540745 => 44
	i64 2262844636196693701, ; 33: Xamarin.AndroidX.DrawerLayout.dll => 0x1f673d352266e6c5 => 55
	i64 2284400282711631002, ; 34: System.Web.Services => 0x1fb3d1f42fd4249a => 104
	i64 2329709569556905518, ; 35: Xamarin.AndroidX.Lifecycle.LiveData.Core.dll => 0x2054ca829b447e2e => 63
	i64 2335503487726329082, ; 36: System.Text.Encodings.Web => 0x2069600c4d9d1cfa => 32
	i64 2337758774805907496, ; 37: System.Runtime.CompilerServices.Unsafe => 0x207163383edbc828 => 26
	i64 2470498323731680442, ; 38: Xamarin.AndroidX.CoordinatorLayout => 0x2248f922dc398cba => 50
	i64 2479423007379663237, ; 39: Xamarin.AndroidX.VectorDrawable.Animated.dll => 0x2268ae16b2cba985 => 83
	i64 2497223385847772520, ; 40: System.Runtime => 0x22a7eb7046413568 => 27
	i64 2547086958574651984, ; 41: Xamarin.AndroidX.Activity.dll => 0x2359121801df4a50 => 36
	i64 2592350477072141967, ; 42: System.Xml.dll => 0x23f9e10627330e8f => 34
	i64 2624866290265602282, ; 43: mscorlib.dll => 0x246d65fbde2db8ea => 14
	i64 2694427813909235223, ; 44: Xamarin.AndroidX.Preference.dll => 0x256487d230fe0617 => 75
	i64 2783046991838674048, ; 45: System.Runtime.CompilerServices.Unsafe.dll => 0x269f5e7e6dc37c80 => 26
	i64 2815524396660695947, ; 46: System.Security.AccessControl => 0x2712c0857f68238b => 29
	i64 2851879596360956261, ; 47: System.Configuration.ConfigurationManager => 0x2793e9620b477965 => 18
	i64 2960931600190307745, ; 48: Xamarin.Forms.Core => 0x2917579a49927da1 => 90
	i64 3017704767998173186, ; 49: Xamarin.Google.Android.Material => 0x29e10a7f7d88a002 => 94
	i64 3289520064315143713, ; 50: Xamarin.AndroidX.Lifecycle.Common => 0x2da6b911e3063621 => 62
	i64 3303437397778967116, ; 51: Xamarin.AndroidX.Annotation.Experimental => 0x2dd82acf985b2a4c => 38
	i64 3311221304742556517, ; 52: System.Numerics.Vectors.dll => 0x2df3d23ba9e2b365 => 25
	i64 3493805808809882663, ; 53: Xamarin.AndroidX.Tracing.Tracing.dll => 0x307c7ddf444f3427 => 81
	i64 3522470458906976663, ; 54: Xamarin.AndroidX.SwipeRefreshLayout => 0x30e2543832f52197 => 80
	i64 3531994851595924923, ; 55: System.Numerics => 0x31042a9aade235bb => 24
	i64 3571415421602489686, ; 56: System.Runtime.dll => 0x319037675df7e556 => 27
	i64 3716579019761409177, ; 57: netstandard.dll => 0x3393f0ed5c8c5c99 => 1
	i64 3727469159507183293, ; 58: Xamarin.AndroidX.RecyclerView => 0x33baa1739ba646bd => 77
	i64 3772598417116884899, ; 59: Xamarin.AndroidX.DynamicAnimation.dll => 0x345af645b473efa3 => 56
	i64 3966267475168208030, ; 60: System.Memory => 0x370b03412596249e => 23
	i64 4525561845656915374, ; 61: System.ServiceModel.Internals => 0x3ece06856b710dae => 105
	i64 4636684751163556186, ; 62: Xamarin.AndroidX.VersionedParcelable.dll => 0x4058d0370893015a => 85
	i64 4782108999019072045, ; 63: Xamarin.AndroidX.AsyncLayoutInflater.dll => 0x425d76cc43bb0a2d => 43
	i64 4794310189461587505, ; 64: Xamarin.AndroidX.Activity => 0x4288cfb749e4c631 => 36
	i64 4795410492532947900, ; 65: Xamarin.AndroidX.SwipeRefreshLayout.dll => 0x428cb86f8f9b7bbc => 80
	i64 4853321196694829351, ; 66: System.Runtime.Loader.dll => 0x435a75ea15de7927 => 28
	i64 5032256205035195147, ; 67: MySql.Data.dll => 0x45d62a5b3fe0cb0b => 15
	i64 5142919913060024034, ; 68: Xamarin.Forms.Platform.Android.dll => 0x475f52699e39bee2 => 91
	i64 5203618020066742981, ; 69: Xamarin.Essentials => 0x4836f704f0e652c5 => 89
	i64 5205316157927637098, ; 70: Xamarin.AndroidX.LocalBroadcastManager => 0x483cff7778e0c06a => 69
	i64 5290786973231294105, ; 71: System.Runtime.Loader => 0x496ca6b869b72699 => 28
	i64 5348796042099802469, ; 72: Xamarin.AndroidX.Media => 0x4a3abda9415fc165 => 70
	i64 5376510917114486089, ; 73: Xamarin.AndroidX.VectorDrawable.Animated => 0x4a9d3431719e5d49 => 83
	i64 5408338804355907810, ; 74: Xamarin.AndroidX.Transition => 0x4b0e477cea9840e2 => 82
	i64 5451019430259338467, ; 75: Xamarin.AndroidX.ConstraintLayout.dll => 0x4ba5e94a845c2ce3 => 49
	i64 5507995362134886206, ; 76: System.Core.dll => 0x4c705499688c873e => 19
	i64 5692067934154308417, ; 77: Xamarin.AndroidX.ViewPager2.dll => 0x4efe49a0d4a8bb41 => 87
	i64 5757522595884336624, ; 78: Xamarin.AndroidX.Concurrent.Futures.dll => 0x4fe6d44bd9f885f0 => 47
	i64 5812387745074149618, ; 79: K4os.Compression.LZ4.dll => 0x50a9bfdbd9fa78f2 => 9
	i64 5814345312393086621, ; 80: Xamarin.AndroidX.Preference => 0x50b0b44182a5c69d => 75
	i64 5896680224035167651, ; 81: Xamarin.AndroidX.Lifecycle.LiveData.dll => 0x51d5376bfbafdda3 => 64
	i64 6085203216496545422, ; 82: Xamarin.Forms.Platform.dll => 0x5472fc15a9574e8e => 92
	i64 6086316965293125504, ; 83: FormsViewGroup.dll => 0x5476f10882baef80 => 6
	i64 6222399776351216807, ; 84: System.Text.Json.dll => 0x565a67a0ffe264a7 => 33
	i64 6319713645133255417, ; 85: Xamarin.AndroidX.Lifecycle.Runtime => 0x57b42213b45b52f9 => 65
	i64 6401687960814735282, ; 86: Xamarin.AndroidX.Lifecycle.LiveData.Core => 0x58d75d486341cfb2 => 63
	i64 6504860066809920875, ; 87: Xamarin.AndroidX.Browser.dll => 0x5a45e7c43bd43d6b => 44
	i64 6548213210057960872, ; 88: Xamarin.AndroidX.CustomView.dll => 0x5adfed387b066da8 => 53
	i64 6591024623626361694, ; 89: System.Web.Services.dll => 0x5b7805f9751a1b5e => 104
	i64 6617685658146568858, ; 90: System.Text.Encoding.CodePages => 0x5bd6be0b4905fa9a => 106
	i64 6659513131007730089, ; 91: Xamarin.AndroidX.Legacy.Support.Core.UI.dll => 0x5c6b57e8b6c3e1a9 => 59
	i64 6876862101832370452, ; 92: System.Xml.Linq => 0x5f6f85a57d108914 => 35
	i64 6894844156784520562, ; 93: System.Numerics.Vectors => 0x5faf683aead1ad72 => 25
	i64 7036436454368433159, ; 94: Xamarin.AndroidX.Legacy.Support.V4.dll => 0x61a671acb33d5407 => 61
	i64 7103753931438454322, ; 95: Xamarin.AndroidX.Interpolator.dll => 0x62959a90372c7632 => 58
	i64 7338192458477945005, ; 96: System.Reflection => 0x65d67f295d0740ad => 111
	i64 7451202609009583483, ; 97: K4os.Hash.xxHash => 0x6767fd4b737ae57b => 11
	i64 7488575175965059935, ; 98: System.Xml.Linq.dll => 0x67ecc3724534ab5f => 35
	i64 7635363394907363464, ; 99: Xamarin.Forms.Core.dll => 0x69f6428dc4795888 => 90
	i64 7637365915383206639, ; 100: Xamarin.Essentials.dll => 0x69fd5fd5e61792ef => 89
	i64 7654504624184590948, ; 101: System.Net.Http => 0x6a3a4366801b8264 => 2
	i64 7820441508502274321, ; 102: System.Data => 0x6c87ca1e14ff8111 => 97
	i64 7836164640616011524, ; 103: Xamarin.AndroidX.AppCompat.AppCompatResources => 0x6cbfa6390d64d704 => 39
	i64 8044118961405839122, ; 104: System.ComponentModel.Composition => 0x6fa2739369944712 => 103
	i64 8083354569033831015, ; 105: Xamarin.AndroidX.Lifecycle.Common.dll => 0x702dd82730cad267 => 62
	i64 8087206902342787202, ; 106: System.Diagnostics.DiagnosticSource => 0x703b87d46f3aa082 => 20
	i64 8103644804370223335, ; 107: System.Data.DataSetExtensions.dll => 0x7075ee03be6d50e7 => 99
	i64 8167236081217502503, ; 108: Java.Interop.dll => 0x7157d9f1a9b8fd27 => 8
	i64 8398329775253868912, ; 109: Xamarin.AndroidX.ConstraintLayout.Core.dll => 0x748cdc6f3097d170 => 48
	i64 8400357532724379117, ; 110: Xamarin.AndroidX.Navigation.UI.dll => 0x749410ab44503ded => 74
	i64 8476857680833348370, ; 111: System.Security.Permissions.dll => 0x75a3d925fd9d0312 => 30
	i64 8601935802264776013, ; 112: Xamarin.AndroidX.Transition.dll => 0x7760370982b4ed4d => 82
	i64 8626175481042262068, ; 113: Java.Interop => 0x77b654e585b55834 => 8
	i64 8639588376636138208, ; 114: Xamarin.AndroidX.Navigation.Runtime => 0x77e5fbdaa2fda2e0 => 73
	i64 8684531736582871431, ; 115: System.IO.Compression.FileSystem => 0x7885a79a0fa0d987 => 102
	i64 8725526185868997716, ; 116: System.Diagnostics.DiagnosticSource.dll => 0x79174bd613173454 => 20
	i64 9286073997824813334, ; 117: BouncyCastle.Cryptography => 0x80dec319ee56e916 => 4
	i64 9312692141327339315, ; 118: Xamarin.AndroidX.ViewPager2 => 0x813d54296a634f33 => 87
	i64 9324707631942237306, ; 119: Xamarin.AndroidX.AppCompat => 0x8168042fd44a7c7a => 40
	i64 9584643793929893533, ; 120: System.IO.dll => 0x85037ebfbbd7f69d => 112
	i64 9662334977499516867, ; 121: System.Numerics.dll => 0x8617827802b0cfc3 => 24
	i64 9678050649315576968, ; 122: Xamarin.AndroidX.CoordinatorLayout.dll => 0x864f57c9feb18c88 => 50
	i64 9711637524876806384, ; 123: Xamarin.AndroidX.Media.dll => 0x86c6aadfd9a2c8f0 => 70
	i64 9808709177481450983, ; 124: Mono.Android.dll => 0x881f890734e555e7 => 13
	i64 9825649861376906464, ; 125: Xamarin.AndroidX.Concurrent.Futures => 0x885bb87d8abc94e0 => 47
	i64 9834056768316610435, ; 126: System.Transactions.dll => 0x8879968718899783 => 98
	i64 9998632235833408227, ; 127: Mono.Security => 0x8ac2470b209ebae3 => 109
	i64 9998685624638532270, ; 128: K4os.Hash.xxHash.dll => 0x8ac27799ad626aae => 11
	i64 10038780035334861115, ; 129: System.Net.Http.dll => 0x8b50e941206af13b => 2
	i64 10229024438826829339, ; 130: Xamarin.AndroidX.CustomView => 0x8df4cb880b10061b => 53
	i64 10376576884623852283, ; 131: Xamarin.AndroidX.Tracing.Tracing => 0x900101b2f888c2fb => 81
	i64 10430153318873392755, ; 132: Xamarin.AndroidX.Core => 0x90bf592ea44f6673 => 51
	i64 10447083246144586668, ; 133: Microsoft.Bcl.AsyncInterfaces.dll => 0x90fb7edc816203ac => 12
	i64 10697692875526994780, ; 134: crmV1.Android => 0x9475d6fdd2037b5c => 0
	i64 10847732767863316357, ; 135: Xamarin.AndroidX.Arch.Core.Common => 0x968ae37a86db9f85 => 41
	i64 10885087467875303060, ; 136: K4os.Compression.LZ4.Streams => 0x970f99615fc37e94 => 10
	i64 11023048688141570732, ; 137: System.Core => 0x98f9bc61168392ac => 19
	i64 11037814507248023548, ; 138: System.Xml => 0x992e31d0412bf7fc => 34
	i64 11122995063473561350, ; 139: Xamarin.CommunityToolkit.dll => 0x9a5cd113fcc3df06 => 88
	i64 11162124722117608902, ; 140: Xamarin.AndroidX.ViewPager => 0x9ae7d54b986d05c6 => 86
	i64 11340910727871153756, ; 141: Xamarin.AndroidX.CursorAdapter => 0x9d630238642d465c => 52
	i64 11341245327015630248, ; 142: System.Configuration.ConfigurationManager.dll => 0x9d643289535355a8 => 18
	i64 11392833485892708388, ; 143: Xamarin.AndroidX.Print.dll => 0x9e1b79b18fcf6824 => 76
	i64 11513602507638267977, ; 144: System.IO.Pipelines.dll => 0x9fc8887aa0d36049 => 22
	i64 11529969570048099689, ; 145: Xamarin.AndroidX.ViewPager.dll => 0xa002ae3c4dc7c569 => 86
	i64 11578238080964724296, ; 146: Xamarin.AndroidX.Legacy.Support.V4 => 0xa0ae2a30c4cd8648 => 61
	i64 11580057168383206117, ; 147: Xamarin.AndroidX.Annotation => 0xa0b4a0a4103262e5 => 37
	i64 11597940890313164233, ; 148: netstandard => 0xa0f429ca8d1805c9 => 1
	i64 11672361001936329215, ; 149: Xamarin.AndroidX.Interpolator => 0xa1fc8e7d0a8999ff => 58
	i64 12011556116648931059, ; 150: System.Security.Cryptography.ProtectedData => 0xa6b19ea5ec87aef3 => 107
	i64 12048625042366043698, ; 151: crmV1 => 0xa73550a3c21a9632 => 5
	i64 12063623837170009990, ; 152: System.Security => 0xa76a99f6ce740786 => 108
	i64 12102847907131387746, ; 153: System.Buffers => 0xa7f5f40c43256f62 => 17
	i64 12137774235383566651, ; 154: Xamarin.AndroidX.VectorDrawable => 0xa872095bbfed113b => 84
	i64 12145679461940342714, ; 155: System.Text.Json => 0xa88e1f1ebcb62fba => 33
	i64 12147625802338281747, ; 156: crmV1.Android.dll => 0xa895094e6cbe2913 => 0
	i64 12313367145828839434, ; 157: System.IO.Pipelines => 0xaae1de2e1c17f00a => 22
	i64 12451044538927396471, ; 158: Xamarin.AndroidX.Fragment.dll => 0xaccaff0a2955b677 => 57
	i64 12466513435562512481, ; 159: Xamarin.AndroidX.Loader.dll => 0xad01f3eb52569061 => 68
	i64 12487638416075308985, ; 160: Xamarin.AndroidX.DocumentFile.dll => 0xad4d00fa21b0bfb9 => 54
	i64 12538491095302438457, ; 161: Xamarin.AndroidX.CardView.dll => 0xae01ab382ae67e39 => 45
	i64 12550732019250633519, ; 162: System.IO.Compression => 0xae2d28465e8e1b2f => 101
	i64 12700543734426720211, ; 163: Xamarin.AndroidX.Collection => 0xb041653c70d157d3 => 46
	i64 12708238894395270091, ; 164: System.IO => 0xb05cbbf17d3ba3cb => 112
	i64 12963446364377008305, ; 165: System.Drawing.Common.dll => 0xb3e769c8fd8548b1 => 100
	i64 13162471042547327635, ; 166: System.Security.Permissions => 0xb6aa7dace9662293 => 30
	i64 13370592475155966277, ; 167: System.Runtime.Serialization => 0xb98de304062ea945 => 3
	i64 13401370062847626945, ; 168: Xamarin.AndroidX.VectorDrawable.dll => 0xb9fb3b1193964ec1 => 84
	i64 13404347523447273790, ; 169: Xamarin.AndroidX.ConstraintLayout.Core => 0xba05cf0da4f6393e => 48
	i64 13454009404024712428, ; 170: Xamarin.Google.Guava.ListenableFuture => 0xbab63e4543a86cec => 95
	i64 13491513212026656886, ; 171: Xamarin.AndroidX.Arch.Core.Runtime.dll => 0xbb3b7bc905569876 => 42
	i64 13572454107664307259, ; 172: Xamarin.AndroidX.RecyclerView.dll => 0xbc5b0b19d99f543b => 77
	i64 13647894001087880694, ; 173: System.Data.dll => 0xbd670f48cb071df6 => 97
	i64 13710614125866346983, ; 174: System.Security.AccessControl.dll => 0xbe45e2e7d0b769e7 => 29
	i64 13959074834287824816, ; 175: Xamarin.AndroidX.Fragment => 0xc1b8989a7ad20fb0 => 57
	i64 13967638549803255703, ; 176: Xamarin.Forms.Platform.Android => 0xc1d70541e0134797 => 91
	i64 14124974489674258913, ; 177: Xamarin.AndroidX.CardView => 0xc405fd76067d19e1 => 45
	i64 14172845254133543601, ; 178: Xamarin.AndroidX.MultiDex => 0xc4b00faaed35f2b1 => 71
	i64 14261073672896646636, ; 179: Xamarin.AndroidX.Print => 0xc5e982f274ae0dec => 76
	i64 14327695147300244862, ; 180: System.Reflection.dll => 0xc6d632d338eb4d7e => 111
	i64 14486659737292545672, ; 181: Xamarin.AndroidX.Lifecycle.LiveData => 0xc90af44707469e88 => 64
	i64 14551742072151931844, ; 182: System.Text.Encodings.Web.dll => 0xc9f22c50f1b8fbc4 => 32
	i64 14644440854989303794, ; 183: Xamarin.AndroidX.LocalBroadcastManager.dll => 0xcb3b815e37daeff2 => 69
	i64 14792063746108907174, ; 184: Xamarin.Google.Guava.ListenableFuture.dll => 0xcd47f79af9c15ea6 => 95
	i64 14852515768018889994, ; 185: Xamarin.AndroidX.CursorAdapter.dll => 0xce1ebc6625a76d0a => 52
	i64 14912225920358050525, ; 186: System.Security.Principal.Windows => 0xcef2de7759506add => 31
	i64 14935719434541007538, ; 187: System.Text.Encoding.CodePages.dll => 0xcf4655b160b702b2 => 106
	i64 14987728460634540364, ; 188: System.IO.Compression.dll => 0xcfff1ba06622494c => 101
	i64 14988210264188246988, ; 189: Xamarin.AndroidX.DocumentFile => 0xd000d1d307cddbcc => 54
	i64 15370334346939861994, ; 190: Xamarin.AndroidX.Core.dll => 0xd54e65a72c560bea => 51
	i64 15582737692548360875, ; 191: Xamarin.AndroidX.Lifecycle.ViewModelSavedState => 0xd841015ed86f6aab => 67
	i64 15609085926864131306, ; 192: System.dll => 0xd89e9cf3334914ea => 21
	i64 15620612276725577442, ; 193: BouncyCastle.Cryptography.dll => 0xd8c7901aa85576e2 => 4
	i64 15777549416145007739, ; 194: Xamarin.AndroidX.SlidingPaneLayout.dll => 0xdaf51d99d77eb47b => 79
	i64 15810740023422282496, ; 195: Xamarin.Forms.Xaml => 0xdb6b08484c22eb00 => 93
	i64 15963349826457351533, ; 196: System.Threading.Tasks.Extensions => 0xdd893616f748b56d => 110
	i64 16154507427712707110, ; 197: System => 0xe03056ea4e39aa26 => 21
	i64 16337011941688632206, ; 198: System.Security.Principal.Windows.dll => 0xe2b8b9cdc3aa638e => 31
	i64 16565028646146589191, ; 199: System.ComponentModel.Composition.dll => 0xe5e2cdc9d3bcc207 => 103
	i64 16621146507174665210, ; 200: Xamarin.AndroidX.ConstraintLayout => 0xe6aa2caf87dedbfa => 49
	i64 16637862623548895820, ; 201: K4os.Compression.LZ4 => 0xe6e58fe7aa61724c => 9
	i64 16677317093839702854, ; 202: Xamarin.AndroidX.Navigation.UI => 0xe771bb8960dd8b46 => 74
	i64 16822611501064131242, ; 203: System.Data.DataSetExtensions => 0xe975ec07bb5412aa => 99
	i64 16833383113903931215, ; 204: mscorlib => 0xe99c30c1484d7f4f => 14
	i64 16873478996345296124, ; 205: K4os.Compression.LZ4.Streams.dll => 0xea2aa3bf662d14fc => 10
	i64 16875039703479327938, ; 206: crmV1.dll => 0xea302f33d87f9cc2 => 5
	i64 17024911836938395553, ; 207: Xamarin.AndroidX.Annotation.Experimental.dll => 0xec44a31d250e5fa1 => 38
	i64 17031351772568316411, ; 208: Xamarin.AndroidX.Navigation.Common.dll => 0xec5b843380a769fb => 72
	i64 17037200463775726619, ; 209: Xamarin.AndroidX.Legacy.Support.Core.Utils => 0xec704b8e0a78fc1b => 60
	i64 17523946658117960076, ; 210: System.Security.Cryptography.ProtectedData.dll => 0xf33190a3c403c18c => 107
	i64 17544493274320527064, ; 211: Xamarin.AndroidX.AsyncLayoutInflater => 0xf37a8fada41aded8 => 43
	i64 17553799493972570483, ; 212: Google.Protobuf.dll => 0xf39b9fa2c0aab173 => 7
	i64 17704177640604968747, ; 213: Xamarin.AndroidX.Loader => 0xf5b1dfc36cac272b => 68
	i64 17710060891934109755, ; 214: Xamarin.AndroidX.Lifecycle.ViewModel => 0xf5c6c68c9e45303b => 66
	i64 17838668724098252521, ; 215: System.Buffers.dll => 0xf78faeb0f5bf3ee9 => 17
	i64 17882897186074144999, ; 216: FormsViewGroup => 0xf82cd03e3ac830e7 => 6
	i64 17892495832318972303, ; 217: Xamarin.Forms.Xaml.dll => 0xf84eea293687918f => 93
	i64 17928294245072900555, ; 218: System.IO.Compression.FileSystem.dll => 0xf8ce18a0b24011cb => 102
	i64 17979120673405951447, ; 219: ZstdSharp => 0xf982aafeb83e5dd7 => 96
	i64 18116111925905154859, ; 220: Xamarin.AndroidX.Arch.Core.Runtime => 0xfb695bd036cb632b => 42
	i64 18121036031235206392, ; 221: Xamarin.AndroidX.Navigation.Common => 0xfb7ada42d3d42cf8 => 72
	i64 18129453464017766560, ; 222: System.ServiceModel.Internals.dll => 0xfb98c1df1ec108a0 => 105
	i64 18305135509493619199, ; 223: Xamarin.AndroidX.Navigation.Runtime.dll => 0xfe08e7c2d8c199ff => 73
	i64 18318849532986632368, ; 224: System.Security.dll => 0xfe39a097c37fa8b0 => 108
	i64 18380184030268848184 ; 225: Xamarin.AndroidX.VersionedParcelable => 0xff1387fe3e7b7838 => 85
], align 8
@assembly_image_cache_indices = local_unnamed_addr constant [226 x i32] [
	i32 56, i32 13, i32 46, i32 78, i32 79, i32 96, i32 15, i32 65, ; 0..7
	i32 100, i32 59, i32 55, i32 98, i32 92, i32 109, i32 94, i32 41, ; 8..15
	i32 3, i32 39, i32 67, i32 60, i32 16, i32 23, i32 7, i32 40, ; 16..23
	i32 78, i32 12, i32 37, i32 66, i32 88, i32 110, i32 16, i32 71, ; 24..31
	i32 44, i32 55, i32 104, i32 63, i32 32, i32 26, i32 50, i32 83, ; 32..39
	i32 27, i32 36, i32 34, i32 14, i32 75, i32 26, i32 29, i32 18, ; 40..47
	i32 90, i32 94, i32 62, i32 38, i32 25, i32 81, i32 80, i32 24, ; 48..55
	i32 27, i32 1, i32 77, i32 56, i32 23, i32 105, i32 85, i32 43, ; 56..63
	i32 36, i32 80, i32 28, i32 15, i32 91, i32 89, i32 69, i32 28, ; 64..71
	i32 70, i32 83, i32 82, i32 49, i32 19, i32 87, i32 47, i32 9, ; 72..79
	i32 75, i32 64, i32 92, i32 6, i32 33, i32 65, i32 63, i32 44, ; 80..87
	i32 53, i32 104, i32 106, i32 59, i32 35, i32 25, i32 61, i32 58, ; 88..95
	i32 111, i32 11, i32 35, i32 90, i32 89, i32 2, i32 97, i32 39, ; 96..103
	i32 103, i32 62, i32 20, i32 99, i32 8, i32 48, i32 74, i32 30, ; 104..111
	i32 82, i32 8, i32 73, i32 102, i32 20, i32 4, i32 87, i32 40, ; 112..119
	i32 112, i32 24, i32 50, i32 70, i32 13, i32 47, i32 98, i32 109, ; 120..127
	i32 11, i32 2, i32 53, i32 81, i32 51, i32 12, i32 0, i32 41, ; 128..135
	i32 10, i32 19, i32 34, i32 88, i32 86, i32 52, i32 18, i32 76, ; 136..143
	i32 22, i32 86, i32 61, i32 37, i32 1, i32 58, i32 107, i32 5, ; 144..151
	i32 108, i32 17, i32 84, i32 33, i32 0, i32 22, i32 57, i32 68, ; 152..159
	i32 54, i32 45, i32 101, i32 46, i32 112, i32 100, i32 30, i32 3, ; 160..167
	i32 84, i32 48, i32 95, i32 42, i32 77, i32 97, i32 29, i32 57, ; 168..175
	i32 91, i32 45, i32 71, i32 76, i32 111, i32 64, i32 32, i32 69, ; 176..183
	i32 95, i32 52, i32 31, i32 106, i32 101, i32 54, i32 51, i32 67, ; 184..191
	i32 21, i32 4, i32 79, i32 93, i32 110, i32 21, i32 31, i32 103, ; 192..199
	i32 49, i32 9, i32 74, i32 99, i32 14, i32 10, i32 5, i32 38, ; 200..207
	i32 72, i32 60, i32 107, i32 43, i32 7, i32 68, i32 66, i32 17, ; 208..215
	i32 6, i32 93, i32 102, i32 96, i32 42, i32 72, i32 105, i32 73, ; 216..223
	i32 108, i32 85 ; 224..225
], align 4

@marshal_methods_number_of_classes = local_unnamed_addr constant i32 0, align 4

; marshal_methods_class_cache
@marshal_methods_class_cache = global [0 x %struct.MarshalMethodsManagedClass] [
], align 8; end of 'marshal_methods_class_cache' array


@get_function_pointer = internal unnamed_addr global void (i32, i32, i32, i8**)* null, align 8

; Function attributes: "frame-pointer"="non-leaf" "min-legal-vector-width"="0" mustprogress nofree norecurse nosync "no-trapping-math"="true" nounwind sspstrong "stack-protector-buffer-size"="8" "target-cpu"="generic" "target-features"="+neon,+outline-atomics" uwtable willreturn writeonly
define void @xamarin_app_init (void (i32, i32, i32, i8**)* %fn) local_unnamed_addr #0
{
	store void (i32, i32, i32, i8**)* %fn, void (i32, i32, i32, i8**)** @get_function_pointer, align 8
	ret void
}

; Names of classes in which marshal methods reside
@mm_class_names = local_unnamed_addr constant [0 x i8*] zeroinitializer, align 8
@__MarshalMethodName_name.0 = internal constant [1 x i8] c"\00", align 1

; mm_method_names
@mm_method_names = local_unnamed_addr constant [1 x %struct.MarshalMethodName] [
	; 0
	%struct.MarshalMethodName {
		i64 0, ; id 0x0; name: 
		i8* getelementptr inbounds ([1 x i8], [1 x i8]* @__MarshalMethodName_name.0, i32 0, i32 0); name
	}
], align 8; end of 'mm_method_names' array


attributes #0 = { "min-legal-vector-width"="0" mustprogress nofree norecurse nosync "no-trapping-math"="true" nounwind sspstrong "stack-protector-buffer-size"="8" uwtable willreturn writeonly "frame-pointer"="non-leaf" "target-cpu"="generic" "target-features"="+neon,+outline-atomics" }
attributes #1 = { "min-legal-vector-width"="0" mustprogress "no-trapping-math"="true" nounwind sspstrong "stack-protector-buffer-size"="8" uwtable "frame-pointer"="non-leaf" "target-cpu"="generic" "target-features"="+neon,+outline-atomics" }
attributes #2 = { nounwind }

!llvm.module.flags = !{!0, !1, !2, !3, !4, !5}
!llvm.ident = !{!6}
!0 = !{i32 1, !"wchar_size", i32 4}
!1 = !{i32 7, !"PIC Level", i32 2}
!2 = !{i32 1, !"branch-target-enforcement", i32 0}
!3 = !{i32 1, !"sign-return-address", i32 0}
!4 = !{i32 1, !"sign-return-address-all", i32 0}
!5 = !{i32 1, !"sign-return-address-with-bkey", i32 0}
!6 = !{!"Xamarin.Android remotes/origin/d17-5 @ 45b0e144f73b2c8747d8b5ec8cbd3b55beca67f0"}
!llvm.linker.options = !{}
