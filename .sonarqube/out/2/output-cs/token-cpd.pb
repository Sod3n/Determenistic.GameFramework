◊
u/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/UserIdProvider.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public		 
class		 
UserIdProvider		 
:		 
IUserIdProvider		 -
{

 
public 

string 
? 
	GetUserId 
(  
HubConnectionContext 1

connection2 <
)< =
{ 
return 

connection 
. 
GetHttpContext (
(( )
)) *
?* +
.+ ,
Request, 3
.3 4
Query4 9
[9 :
$str: B
]B C
;C D
} 
} ä
z/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/SyncGameStateAction.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class 
SyncGameStateAction  
<  !

TGameState! +
>+ ,
:- .
NetworkAction/ <
<< =

TGameState= G
,G H
SyncGameStateActionI \
<\ ]

TGameState] g
>g h
>h i
where 	

TGameState
 
: 
NetworkGameState '
{ 
public 

List 
< 
INetworkAction 
> 
History  '
{( )
get* -
;- .
set/ 2
;2 3
}4 5
=6 7
new8 ;
(; <
)< =
;= >
public 

int 
Seed 
{ 
get 
; 
set 
; 
}  !
public 

override 
NetworkThread !
Thread" (
=>) +
NetworkThread, 9
.9 :
Main: >
;> ?
public 

SyncGameStateAction 
( 
)  
{ 
} 
public 

SyncGameStateAction 
( 

TGameState )
state* /
)/ 0
{ 
History 
= 
state 
. 
History 
;  
Seed 
= 
state 
.  
RandomProviderDomain )
.) *
Seed* .
;. /
} 
	protected   
override   
void   
ExecuteProcess   *
(  * +

TGameState  + 5
	gameState  6 ?
)  ? @
{!! 
	gameState## 
.##  
RandomProviderDomain## &
.##& '
Reset##' ,
(##, -
Seed##- 1
)##1 2
;##2 3
var&& 
executor&& 
=&& 
new&& !
NetworkActionExecutor&& 0
(&&0 1
	gameState&&1 :
.&&: ;
Registry&&; C
)&&C D
;&&D E
foreach'' 
('' 
var'' 
action'' 
in'' 
History'' &
)''& '
{(( 	
action)) 
.)) 
Execute)) 
()) 
	gameState)) $
)))$ %
;))% &
}** 	
}++ 
},, €
Ç/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/ServiceCollectionExtensions.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
static 
class '
ServiceCollectionExtensions /
{		 
public 

static 
IServiceCollection $ 
AddMultiplayerServer% 9
<9 :

TGameState: D
>D E
(E F
this 
IServiceCollection 
services  (
,( )
Func 
< 
Guid 
, 

TGameState 
> 
gameStateFactory /
)/ 0
where 

TGameState 
: 
NetworkGameState +
{ 
services 
. 
AddSingleton 
< 
ServerDomain *
>* +
(+ ,
), -
;- .
services 
. 
AddSingleton 
< 
IGameStateFactory /
</ 0

TGameState0 :
>: ;
>; <
(< =
_ 
=> 
new #
DefaultGameStateFactory ,
<, -

TGameState- 7
>7 8
(8 9
gameStateFactory9 I
)I J
)J K
;K L
services 
. 
AddSingleton 
< 
MatchManager *
<* +

TGameState+ 5
>5 6
>6 7
(7 8
)8 9
;9 :
services 
. 

AddSignalR 
( 
) 
; 
return 
services 
; 
} 
public   

static   
IServiceCollection   $ 
AddMultiplayerServer  % 9
<  9 :

TGameState  : D
,  D E
TFactory  F N
>  N O
(  O P
this  P T
IServiceCollection  U g
services  h p
)  p q
where!! 

TGameState!! 
:!! 
NetworkGameState!! +
where"" 
TFactory"" 
:"" 
class"" 
,"" 
IGameStateFactory""  1
<""1 2

TGameState""2 <
>""< =
{## 
services$$ 
.$$ 
AddSingleton$$ 
<$$ 
ServerDomain$$ *
>$$* +
($$+ ,
)$$, -
;$$- .
services%% 
.%% 
AddSingleton%% 
<%% 
IGameStateFactory%% /
<%%/ 0

TGameState%%0 :
>%%: ;
,%%; <
TFactory%%= E
>%%E F
(%%F G
)%%G H
;%%H I
services&& 
.&& 
AddSingleton&& 
<&& 
MatchManager&& *
<&&* +

TGameState&&+ 5
>&&5 6
>&&6 7
(&&7 8
)&&8 9
;&&9 :
services'' 
.'' 

AddSignalR'' 
('' 
)'' 
;'' 
return)) 
services)) 
;)) 
}** 
}++ ’
s/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/ServerDomain.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class 
ServerDomain 
: 

RootDomain &
{ 
public		 
static		 
ServerDomain		 
Instance		 $
{		% &
get		' *
;		* +
private		, 3
set		4 7
;		7 8
}		9 :
=		; <
null		= A
!		A B
;		B C
public 
Guid 
ServerId 
{ 
get 
; 
private $
set% (
;( )
}* +
=, -
Guid. 2
.2 3
NewGuid3 :
(: ;
); <
;< =
public 
NetworkSyncManager 
NetworkSyncManager -
{. /
get0 3
;3 4
}5 6
public 
GameLoop 
GameLoop 
{ 
get 
;  
}! "
public 
bool 
RelayOnlyMode 
{ 
get  
;  !
set" %
;% &
}' (
=) *
false+ 0
;0 1
public 
ServerDomain 
( 
) 
{ 
Instance 

= 
this 
; 
NetworkSyncManager 
= 
new 
NetworkSyncManager -
(- .
this. 2
)2 3
;3 4
new 
NetworkSyncManager 
. )
CollectNetworkActionsReaction 6
(6 7
NetworkSyncManager7 I
,I J
thisK O
)O P
.P Q
AddToQ V
(V W
DisposablesW b
)b c
;c d
GameLoop 

= 
new 
GameLoop 
( 
this 
) 
;  
GameLoop 

.
 
SetTargetFps 
( 
$num 
) 
; 
_ 
= 
GameLoop 
. 
Start 
( 
) 
; 
new 
Reaction 
< 
BranchDomain 
, 
INetworkAction +
>+ ,
(, -
this- 1
)1 2
.   
Prepare   
(   
(   
_   
,   
action   
)   
=>   
{!! 
action"" 

.""
 

ExecutorId"" 
??="" 
ServerId"" "
;""" #
action## 

.##
 
IsServer## 
=## 
true## 
;## 
}$$ 
)$$ 
.%% 
AddTo%% 	
(%%	 

Disposables%%
 
)%% 
;%% 
}&& 
}'' “
q/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/SendAction.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class 

SendAction 
( 
INetworkAction &
action' -
,- .

LeafDomain/ 9
target: @
)@ A
:B C
	DARActionD M
<M N

RootDomainN X
,X Y

SendActionZ d
>d e
{ 
	protected 
override 
void 
ExecuteProcess *
(* +

RootDomain+ 5
domain6 <
)< =
{ 
var 
networkSyncManager 
=  
domain! '
.' (
GetFirst( 0
<0 1
NetworkSyncManager1 C
>C D
(D E
)E F
;F G
if 

( 
networkSyncManager 
== !
null" &
)& '
{ 	
throw 
new %
InvalidOperationException /
(/ 0
$str0 ]
)] ^
;^ _
} 	
networkSyncManager 
. 
AddPendingAction +
(+ ,
target, 2
,2 3
action4 :
): ;
;; <
} 
} œ3
t/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/NetworkThread.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
sealed 
class 
NetworkThread !
:" #

IEquatable$ .
<. /
NetworkThread/ <
>< =
{ 
private		 
static		 
readonly		 

Dictionary		 &
<		& '
int		' *
,		* +
NetworkThread		, 9
>		9 :
	_registry		; D
=		E F
new		G J
(		J K
)		K L
;		L M
private

 
static

 
readonly

 
object

 "
_lock

# (
=

) *
new

+ .
(

. /
)

/ 0
;

0 1
public 

int 
Id 
{ 
get 
; 
} 
public 

string 
Name 
{ 
get 
; 
} 
private 
NetworkThread 
( 
int 
id  
,  !
string" (
name) -
)- .
{ 
Id 

= 
id 
; 
Name 
= 
name 
; 
} 
public 

static 
NetworkThread 
Register  (
(( )
int) ,
id- /
,/ 0
string1 7
name8 <
)< =
{ 
lock 
( 
_lock 
) 
{ 	
if 
( 
	_registry 
. 
ContainsKey %
(% &
id& (
)( )
)) *
throw   
new   %
InvalidOperationException   3
(  3 4
$"  4 6
$str  6 L
{  L M
id  M O
}  O P
$str  P g
"  g h
)  h i
;  i j
var"" 
thread"" 
="" 
new"" 
NetworkThread"" *
(""* +
id""+ -
,""- .
name""/ 3
)""3 4
;""4 5
	_registry## 
[## 
id## 
]## 
=## 
thread## "
;##" #
return$$ 
thread$$ 
;$$ 
}%% 	
}&& 
public++ 

static++ 
NetworkThread++ 
?++  
GetById++! (
(++( )
int++) ,
id++- /
)++/ 0
{,, 
lock-- 
(-- 
_lock-- 
)-- 
{.. 	
return// 
	_registry// 
.// 
TryGetValue// (
(//( )
id//) +
,//+ ,
out//- 0
var//1 4
thread//5 ;
)//; <
?//= >
thread//? E
://F G
null//H L
;//L M
}00 	
}11 
public66 

static66 
IEnumerable66 
<66 
NetworkThread66 +
>66+ ,
GetAll66- 3
(663 4
)664 5
{77 
lock88 
(88 
_lock88 
)88 
{99 	
return:: 
	_registry:: 
.:: 
Values:: #
.::# $
ToList::$ *
(::* +
)::+ ,
;::, -
};; 	
}<< 
public?? 

static?? 
readonly?? 
NetworkThread?? (
Main??) -
=??. /
Register??0 8
(??8 9
$num??9 :
,??: ;
$str??< B
)??B C
;??C D
public@@ 

static@@ 
readonly@@ 
NetworkThread@@ (
PingPong@@) 1
=@@2 3
Register@@4 <
(@@< =
$num@@= >
,@@> ?
$str@@@ J
)@@J K
;@@K L
publicAA 

staticAA 
readonlyAA 
NetworkThreadAA (
ChatAA) -
=AA. /
RegisterAA0 8
(AA8 9
$numAA9 :
,AA: ;
$strAA< B
)AAB C
;AAC D
publicDD 

boolDD 
EqualsDD 
(DD 
NetworkThreadDD $
?DD$ %
otherDD& +
)DD+ ,
{EE 
ifFF 

(FF 
ReferenceEqualsFF 
(FF 
nullFF  
,FF  !
otherFF" '
)FF' (
)FF( )
returnFF* 0
falseFF1 6
;FF6 7
ifGG 

(GG 
ReferenceEqualsGG 
(GG 
thisGG  
,GG  !
otherGG" '
)GG' (
)GG( )
returnGG* 0
trueGG1 5
;GG5 6
returnHH 
IdHH 
==HH 
otherHH 
.HH 
IdHH 
;HH 
}II 
publicKK 

overrideKK 
boolKK 
EqualsKK 
(KK  
objectKK  &
?KK& '
objKK( +
)KK+ ,
=>KK- /
EqualsKK0 6
(KK6 7
objKK7 :
asKK; =
NetworkThreadKK> K
)KKK L
;KKL M
publicLL 

overrideLL 
intLL 
GetHashCodeLL #
(LL# $
)LL$ %
=>LL& (
IdLL) +
;LL+ ,
publicMM 

overrideMM 
stringMM 
ToStringMM #
(MM# $
)MM$ %
=>MM& (
$"MM) +
{MM+ ,
NameMM, 0
}MM0 1
$strMM1 7
{MM7 8
IdMM8 :
}MM: ;
$strMM; <
"MM< =
;MM= >
publicOO 

staticOO 
boolOO 
operatorOO 
==OO  "
(OO" #
NetworkThreadOO# 0
?OO0 1
leftOO2 6
,OO6 7
NetworkThreadOO8 E
?OOE F
rightOOG L
)OOL M
=>OON P
EqualsOOQ W
(OOW X
leftOOX \
,OO\ ]
rightOO^ c
)OOc d
;OOd e
publicPP 

staticPP 
boolPP 
operatorPP 
!=PP  "
(PP" #
NetworkThreadPP# 0
?PP0 1
leftPP2 6
,PP6 7
NetworkThreadPP8 E
?PPE F
rightPPG L
)PPL M
=>PPN P
!PPQ R
EqualsPPR X
(PPX Y
leftPPY ]
,PP] ^
rightPP_ d
)PPd e
;PPe f
}QQ ∞*
y/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/NetworkSyncManager.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class 
NetworkSyncManager 
:  !
BranchDomain" .
,. /

IProcessor0 :
{ 
private 

Dictionary 
< 
Guid 
, 
List !
<! "
INetworkAction" 0
>0 1
>1 2"
_pendingActionsByMatch3 I
=J K
newL O
(O P
)P Q
;Q R
private 
float 
_timeSinceLastSync $
=% &
$num' )
;) *
private 
float 
_syncInterval 
=  !
$num" '
;' (
public 

event 
Action 
< 
Guid 
, 
List "
<" #
INetworkAction# 1
>1 2
>2 3
?3 4
OnSync5 ;
;; <
public 

NetworkSyncManager 
( 
BranchDomain *
serverDomain+ 7
,7 8
float9 >
syncInterval? K
=L M
$numN S
)S T
:U V
baseW [
([ \
serverDomain\ h
)h i
{ 
_syncInterval   
=   
syncInterval   $
;  $ %
}!! 
public$$ 

void$$ 
Process$$ 
($$ 
float$$ 
delta$$ #
)$$# $
{%% 
_timeSinceLastSync&& 
+=&& 
delta&& #
;&&# $
if)) 

()) 
_timeSinceLastSync)) 
>=)) !
_syncInterval))" /
)))/ 0
{** 	
Flush++ 
(++ 
)++ 
;++ 
},, 	
}-- 
public// 

void// 
Flush// 
(// 
)// 
{00 
var22 
snapshot22 
=22 "
_pendingActionsByMatch22 -
;22- ."
_pendingActionsByMatch33 
=33  
new33! $

Dictionary33% /
<33/ 0
Guid330 4
,334 5
List336 :
<33: ;
INetworkAction33; I
>33I J
>33J K
(33K L
)33L M
;33M N
_timeSinceLastSync44 
=44 
$num44 
;44  
foreach77 
(77 
var77 
(77 
matchId77 
,77 
actions77 &
)77& '
in77( *
snapshot77+ 3
)773 4
{88 	
if99 
(99 
actions99 
.99 
Count99 
>99 
$num99  !
)99! "
{:: 
OnSync== 
?== 
.== 
Invoke== 
(== 
matchId== &
,==& '
actions==( /
)==/ 0
;==0 1
}>> 
}?? 	
}@@ 
publicBB 

voidBB 
AddPendingActionBB  
(BB  !

LeafDomainBB! +
domainBB, 2
,BB2 3
INetworkActionBB4 B
actionBBC I
)BBI J
{CC 
actionDD 
.DD 
DomainIdDD 
=DD 
domainDD  
.DD  !
IdDD! #
;DD# $
varEE 
matchIdEE 
=EE 
actionEE 
.EE 
MatchIdEE $
;EE$ %
ifFF 

(FF 
!FF "
_pendingActionsByMatchFF #
.FF# $
ContainsKeyFF$ /
(FF/ 0
matchIdFF0 7
)FF7 8
)FF8 9
{GG 	"
_pendingActionsByMatchHH "
[HH" #
matchIdHH# *
]HH* +
=HH, -
newHH. 1
ListHH2 6
<HH6 7
INetworkActionHH7 E
>HHE F
(HHF G
)HHG H
;HHH I
}II 	"
_pendingActionsByMatchJJ 
[JJ 
matchIdJJ &
]JJ& '
.JJ' (
AddJJ( +
(JJ+ ,
actionJJ, 2
)JJ2 3
;JJ3 4
}KK 
publicMM 

classMM )
CollectNetworkActionsReactionMM .
(MM. /
NetworkSyncManagerMM/ A
managerMMB I
,MMI J
ServerDomainMMK W
targetMMX ^
)MM^ _
:MM` a
ReactionMMb j
(MMj k
targetMMk q
)MMq r
,MMr s
IAfterReaction	MMt Ç
<
MMÇ É
BranchDomain
MMÉ è
,
MMè ê
INetworkAction
MMë ü
>
MMü †
{NN 
publicPP 
voidPP 
OnAfterPP 
(PP 
BranchDomainPP (
domainPP) /
,PP/ 0
INetworkActionPP1 ?
actionPP@ F
)PPF G
{QQ 	
ifRR 
(RR 
!RR 
actionRR 
.RR 
SyncToClientRR $
)RR$ %
returnRR& ,
;RR, -
managerSS 
.SS 
AddPendingActionSS $
(SS$ %
domainSS% +
,SS+ ,
actionSS- 3
)SS3 4
;SS4 5
}TT 	
}UU 
}VV ≠
w/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/NetworkGameState.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public		 
class		 
NetworkGameState		 
:		 
BranchDomain		  ,
{

 
public 
MatchIdDomain 
MatchIdDomain #
{$ %
get& )
;) *
}+ ,
public 
DomainRegistry 
Registry 
{  !
get" %
;% &
}' (
public 
HistoryDomain 
HistoryDomain #
{$ %
get& )
;) *
}+ ,
public  
RandomProviderDomain  
RandomProviderDomain 1
{2 3
get4 7
;7 8
}9 :
public 
IdProviderDomain 

IdProvider #
{$ %
get& )
;) *
}+ ,
public 
Guid 
MatchId 
=> 
MatchIdDomain %
.% &
MatchId& -
;- .
public 
List 
< 
INetworkAction 
> 
History $
=>% '
HistoryDomain( 5
.5 6
History6 =
;= >
	protected 

NetworkGameState 
( 
Guid  
matchId! (
,( )
int* -

randomSeed. 8
)8 9
:: ;
base< @
(@ A
nullA E
)E F
{ 
MatchIdDomain 
= 
new 
MatchIdDomain #
(# $
this$ (
,( )
matchId* 1
)1 2
;2 3
Registry 

= 
new 
DomainRegistry 
(  
this  $
)$ %
;% &
HistoryDomain 
= 
new 
HistoryDomain #
(# $
this$ (
)( )
;) * 
RandomProviderDomain 
= 
new  
RandomProviderDomain 1
(1 2
this2 6
,6 7

randomSeed8 B
)B C
;C D

IdProvider 
= 
new 
IdProviderDomain #
(# $
this$ (
)( )
;) *
} 
public 

LeafDomain 
? 
	GetDomain 
( 
int !
id" $
)$ %
=>& (
Registry) 1
.1 2
	GetDomain2 ;
(; <
id< >
)> ?
;? @
public 
bool 
TryGetDomain 
( 
int 
id  
,  !
out" %

LeafDomain& 0
domain1 7
)7 8
=>9 ;
Registry< D
.D E
TryGetDomainE Q
(Q R
idR T
,T U
outV Y
domainZ `
)` a
;a b
}   ê5
|/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/NetworkActionExecutor.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class !
NetworkActionExecutor "
{ 
private 
readonly 
DomainRegistry #
	_registry$ -
;- .
public 

event 
Action 
< 
INetworkAction &
>& '
BeforeAction( 4
;4 5
public 

Func 
< 
INetworkAction 
, 
Action  &
,& '
bool( ,
>, -
?- .
ValidationHook/ =
{> ?
get@ C
;C D
setE H
;H I
}J K
public 
!
NetworkActionExecutor  
(  !
DomainRegistry! /
registry0 8
)8 9
{ 
	_registry 
= 
registry 
; 
} 
public$$ 

bool$$ 
ExecuteAction$$ 
($$ 
INetworkAction$$ ,
action$$- 3
,$$3 4
Guid$$5 9
?$$9 :

executorId$$; E
=$$F G
null$$H L
,$$L M
System$$N T
.$$T U
Action$$U [
<$$[ \
string$$\ b
>$$b c
?$$c d
onError$$e l
=$$m n
null$$o s
)$$s t
{%% 
try&& 
{'' 	

LeafDomain)) 
domain)) 
;)) 
if** 
(** 
action** 
.** 
DomainId** 
==**  "
$num**# $
)**$ %
{++ 
domain-- 
=-- 
	_registry-- "
.--" #
	GetDomain--# ,
(--, -
$num--- .
)--. /
;--/ 0
}.. 
else// 
{00 
domain11 
=11 
	_registry11 "
.11" #
	GetDomain11# ,
(11, -
action11- 3
.113 4
DomainId114 <
)11< =
;11= >
}22 
if44 
(44 
domain44 
==44 
null44 
)44 
{55 
onError66 
?66 
.66 
Invoke66 
(66  
$"66  "
$str66" )
{66) *
action66* 0
.660 1
DomainId661 9
}669 :
$str66: D
"66D E
)66E F
;66F G
return77 
false77 
;77 
}88 
if;; 
(;; 

executorId;; 
.;; 
HasValue;; #
);;# $
{<< 
action== 
.== 

ExecutorId== !
===" #

executorId==$ .
.==. /
Value==/ 4
;==4 5
}>> 
ifAA 
(AA 
ValidationHookAA 
!=AA !
nullAA" &
)AA& '
{BB 
boolCC 
isValidCC 
=CC 
ValidationHookCC -
(CC- .
actionCC. 4
,CC4 5
(CC6 7
)CC7 8
=>CC9 ;
{DD 
BeforeActionEE  
?EE  !
.EE! "
InvokeEE" (
(EE( )
actionEE) /
)EE/ 0
;EE0 1
actionFF 
.FF 
ExecuteFF "
(FF" #
domainFF# )
)FF) *
;FF* +
}GG 
)GG 
;GG 
returnHH 
isValidHH 
;HH 
}II 
elseJJ 
{KK 
BeforeActionMM 
?MM 
.MM 
InvokeMM $
(MM$ %
actionMM% +
)MM+ ,
;MM, -
actionNN 
.NN 
ExecuteNN 
(NN 
domainNN %
)NN% &
;NN& '
returnOO 
trueOO 
;OO 
}PP 
}QQ 	
catchRR 
(RR 
	ExceptionRR 
exRR 
)RR 
{SS 	
onErrorTT 
?TT 
.TT 
InvokeTT 
(TT 
$"TT 
$strTT 5
{TT5 6
actionTT6 <
.TT< =
GetTypeTT= D
(TTD E
)TTE F
.TTF G
NameTTG K
}TTK L
$strTTL N
{TTN O
exTTO Q
.TTQ R
MessageTTR Y
}TTY Z
"TTZ [
)TT[ \
;TT\ ]
returnUU 
falseUU 
;UU 
}VV 	
}WW 
public__ 

int__ 
ExecuteBatch__ 
(__ 
string__ "
actionsJson__# .
,__. /
Guid__0 4
?__4 5

executorId__6 @
=__A B
null__C G
,__G H
System__I O
.__O P
Action__P V
<__V W
string__W ]
>__] ^
?__^ _
onError__` g
=__h i
null__j n
)__n o
{`` 
tryaa 
{bb 	
vardd 
actionsdd 
=dd 
JsonSerializerdd (
.dd( )
FromJsondd) 1
<dd1 2
Listdd2 6
<dd6 7
INetworkActiondd7 E
>ddE F
>ddF G
(ddG H
actionsJsonddH S
)ddS T
;ddT U
ifee 
(ee 
actionsee 
==ee 
nullee 
||ee  "
actionsee# *
.ee* +
Countee+ 0
==ee1 3
$numee4 5
)ee5 6
{ff 
returngg 
$numgg 
;gg 
}hh 
intjj 
successCountjj 
=jj 
$numjj  
;jj  !
foreachkk 
(kk 
varkk 
actionkk 
inkk  "
actionskk# *
)kk* +
{ll 
ifmm 
(mm 
ExecuteActionmm !
(mm! "
actionmm" (
,mm( )

executorIdmm* 4
,mm4 5
onErrormm6 =
)mm= >
)mm> ?
{nn 
successCountoo  
++oo  "
;oo" #
}pp 
}qq 
returnss 
successCountss 
;ss  
}tt 	
catchuu 
(uu 
	Exceptionuu 
exuu 
)uu 
{vv 	
onErrorww 
?ww 
.ww 
Invokeww 
(ww 
$"ww 
$strww 5
{ww5 6
exww6 8
.ww8 9
Messageww9 @
}ww@ A
"wwA B
)wwB C
;wwC D
returnxx 
$numxx 
;xx 
}yy 	
}zz 
}{{ î
t/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/NetworkAction.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
	interface 
INetworkAction 
:  !

IDARAction" ,
{ 
Guid 
? 	

ExecutorId
 
{ 
get 
; 
set 
;  
}! "
Guid		 
MatchId			 
{		 
get		 
;		 
set		 
;		 
}		 
int

 
DomainId

 
{

 
get

 
;

 
set

 
;

 
}

 
bool 
IsServer	 
{ 
get 
; 
set 
; 
} 
NetworkThread 
Thread 
{ 
get 
; 
}  !
int 
	CurrentId 
{ 
get 
; 
set 
; 
} 
bool 
SyncToClient	 
{ 
get 
; 
set  
;  !
}" #
} 
public 
abstract 
class 
NetworkAction #
<# $
TDomain$ +
,+ ,
TAction- 4
>4 5
:6 7
	DARAction8 A
<A B
TDomainB I
,I J
TActionK R
>R S
,S T
INetworkActionU c
whered i
TDomainj q
:r s
classt y
,y z
IDomain	{ Ç
where
É à
TAction
â ê
:
ë í
class
ì ò
,
ò ô
INetworkAction
ö ®
{ 
public 

Guid 
? 

ExecutorId 
{ 
get !
;! "
set# &
;& '
}( )
public 

Guid 
MatchId 
{ 
get 
; 
set "
;" #
}$ %
public 

int 
DomainId 
{ 
get 
; 
set "
;" #
}$ %
public 

bool 
IsServer 
{ 
get 
; 
set  #
;# $
}% &
public 

bool 
SyncToClient 
{ 
get "
;" #
set$ '
;' (
}) *
public 

virtual 
NetworkThread  
Thread! '
{( )
get* -
;- .
	protected/ 8
set9 <
;< =
}> ?
=@ A
NetworkThreadB O
.O P
MainP T
;T U
public 

int 
	CurrentId 
{ 
get 
; 
set  #
;# $
}% &
[ 

JsonIgnore 
] 
public   

virtual   
bool   
IsServerSecret   &
{  ' (
get  ) ,
;  , -
	protected  . 7
set  8 ;
;  ; <
}  = >
=  ? @
false  A F
;  F G
}!! ƒ/
s/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/MatchManager.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public		 
class		 
MatchManager		 
<		 

TGameState		 $
>		$ %
where		& +

TGameState		, 6
:		7 8

LeafDomain		9 C
{

 
private 
readonly	 
ServerDomain 
_serverDomain ,
;, -
private 
readonly	 
IGameStateFactory #
<# $

TGameState$ .
>. /
_gameStateFactory0 A
;A B
private 
readonly	 

Dictionary 
< 
Guid !
,! "

TGameState# -
>- .
_matches/ 7
=8 9
new: =
(= >
)> ?
;? @
private 
readonly	 
object 
_lock 
=  
new! $
($ %
)% &
;& '
public 
event 
Action 
< 
Guid 
, 

TGameState %
>% &
?& '
OnMatchCreated( 6
;6 7
public 
MatchManager 
( 
ServerDomain !
serverDomain" .
,. /
IGameStateFactory0 A
<A B

TGameStateB L
>L M
gameStateFactoryN ^
)^ _
{ 
_serverDomain 
= 
serverDomain 
; 
_gameStateFactory 
= 
gameStateFactory &
;& '
} 
public!! 

TGameState!! 
CreateMatch!! 
(!! 
Guid!! #
matchId!!$ +
)!!+ ,
{"" 
lock## 
(## 
_lock## 
)## 
{$$ 
if%% 
(%% 
_matches%% 
.%% 
ContainsKey%% 
(%% 
matchId%% #
)%%# $
)%%$ %
{&& 
throw'' 	
new''
 %
InvalidOperationException'' '
(''' (
$"''( *
$str''* 0
{''0 1
matchId''1 8
}''8 9
$str''9 H
"''H I
)''I J
;''J K
}(( 
Console** 

.**
 
	WriteLine** 
(** 
$"** 
$str** K
{**K L
matchId**L S
}**S T
"**T U
)**U V
;**V W
var++ 
match++ 
=++ 
_gameStateFactory++  
.++  !
CreateGameState++! 0
(++0 1
matchId++1 8
)++8 9
;++9 :
_matches,, 
[,, 
matchId,, 
],, 
=,, 
match,, 
;,, 
_serverDomain-- 
.-- 
GameLoop-- 
.-- 
Schedule-- "
(--" #
(--# $
)--$ %
=>--& (
_serverDomain--) 6
.--6 7

Subdomains--7 A
.--A B
Add--B E
(--E F
match--F K
)--K L
)--L M
;--M N
OnMatchCreated00 
?00 
.00 
Invoke00 
(00 
matchId00 !
,00! "
match00# (
)00( )
;00) *
Console22 

.22
 
	WriteLine22 
(22 
$"22 
$str22 ,
{22, -
matchId22- 4
}224 5
$str225 @
"22@ A
)22A B
;22B C
return44 	
match44
 
;44 
}55 
}66 
public;; 

TGameState;; 
?;; 
GetMatch;; 
(;; 
Guid;; !
matchId;;" )
);;) *
{<< 
lock== 
(== 
_lock== 
)== 
{>> 
return?? 	
_matches??
 
.?? 
GetValueOrDefault?? $
(??$ %
matchId??% ,
)??, -
;??- .
}@@ 
}AA 
publicFF 
voidFF 
RemoveMatchFF 
(FF 
GuidFF 
matchIdFF %
)FF% &
{GG 
lockHH 
(HH 
_lockHH 
)HH 
{II 
ifJJ 
(JJ 
_matchesJJ 
.JJ 
RemoveJJ 
(JJ 
matchIdJJ 
,JJ 
outJJ  #
varJJ$ '
matchJJ( -
)JJ- .
)JJ. /
{KK 
_serverDomainMM 
.MM 
GameLoopMM 
.MM 
ScheduleMM #
(MM# $
(MM$ %
)MM% &
=>MM' )
{NN 
_serverDomainOO 
.OO 

SubdomainsOO 
.OO 
RemoveOO $
(OO$ %
matchOO% *
)OO* +
;OO+ ,
matchPP 

.PP
 
DisposePP 
(PP 
)PP 
;PP 
}QQ 
)QQ 
;QQ 
}RR 
}SS 
}TT 
publicYY 
IEnumerableYY 
<YY 
GuidYY 
>YY 
GetAllMatchIdsYY (
(YY( )
)YY) *
{ZZ 
lock[[ 
([[ 
_lock[[ 
)[[ 
{\\ 
return]] 	
new]]
 
List]] 
<]] 
Guid]] 
>]] 
(]] 
_matches]] !
.]]! "
Keys]]" &
)]]& '
;]]' (
}^^ 
}__ 
publicdd 
intdd 

MatchCountdd 
{ee 
getff 
{gg 
lockhh 
(hh 	
_lockhh	 
)hh 
{ii 
returnjj 

_matchesjj 
.jj 
Countjj 
;jj 
}kk 
}ll 
}mm 
}nn à
t/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/MatchIdDomain.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class 
MatchIdDomain 
: 
BranchDomain )
{		 
public

 

Guid

 
MatchId

 
{

 
get

 
;

 
}

  
public 

MatchIdDomain 
( 
BranchDomain %
parent& ,
,, -
Guid. 2
matchId3 :
): ;
:< =
base> B
(B C
parentC I
)I J
{ 
MatchId 
= 
matchId 
; 
new 
Reaction 
< 

LeafDomain 
,  
INetworkAction! /
>/ 0
(0 1
parent1 7
)7 8
. 
Prepare 
( 
( 
_ 
, 
action 
)  
=>! #
action$ *
.* +
MatchId+ 2
=3 4
MatchId5 <
)< =
. 
AddTo 
( 
Disposables 
) 
;  
} 
} ä
x/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/IGameStateFactory.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
	interface 
IGameStateFactory "
<" #
out# &

TGameState' 1
>1 2
where3 8

TGameState9 C
:D E

LeafDomainF P
{ 

TGameState 
CreateGameState 
( 
Guid #
matchId$ +
)+ ,
;, -
} u
s/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/GlobalUsings.csﬁé
n/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/GameHub.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
partial 
class 
GameHub 
< 
TMatchManager *
,* +

TGameState, 6
>6 7
:8 9
Hub: =
where 	
TMatchManager
 
: 
MatchManager &
<& '

TGameState' 1
>1 2
where 	

TGameState
 
: 
NetworkGameState '
{ 
	protected 
readonly 
ServerDomain #
ServerDomain$ 0
;0 1
	protected 
readonly 
TMatchManager $
MatchManager% 1
;1 2
private 
static 
readonly  
ConcurrentDictionary 0
<0 1
string1 7
,7 8
(9 :
Guid: >
PlayerId? G
,G H
GuidI M
MatchIdN U
)U V
>V W
ConnectionsX c
=d e
newf i
(i j
)j k
;k l
private 
static 
readonly  
ConcurrentDictionary 0
<0 1
Guid1 5
,5 6
int7 :
>: ;
MatchPlayerCounts< M
=N O
newP S
(S T
)T U
;U V
	protected 
GameHub 
( 
ServerDomain "
serverDomain# /
,/ 0
TMatchManager1 >
matchManager? K
)K L
{ 
ServerDomain 
= 
serverDomain #
;# $
MatchManager 
= 
matchManager #
;# $
} 
public## 

override## 
async## 
Task## 
OnConnectedAsync## /
(##/ 0
)##0 1
{$$ 
var&& 
httpContext&& 
=&& 
Context&& !
.&&! "
GetHttpContext&&" 0
(&&0 1
)&&1 2
;&&2 3
if'' 

('' 
httpContext'' 
=='' 
null'' 
)''  
{(( 	
Context)) 
.)) 
Abort)) 
()) 
))) 
;)) 
return** 
;** 
}++ 	
var-- 
userId-- 
=-- 
Guid-- 
.-- 
Parse-- 
(--  
httpContext--  +
.--+ ,
Request--, 3
.--3 4
Query--4 9
[--9 :
$str--: B
]--B C
)--C D
;--D E
var.. 
matchId.. 
=.. 
Guid.. 
... 
Parse..  
(..  !
httpContext..! ,
..., -
Request..- 4
...4 5
Query..5 :
[..: ;
$str..; D
]..D E
)..E F
;..F G
Connections11 
[11 
Context11 
.11 
ConnectionId11 (
]11( )
=11* +
(11, -
userId11- 3
,113 4
matchId115 <
)11< =
;11= >
var44 
	gameState44 
=44 
MatchManager44 $
.44$ %
GetMatch44% -
(44- .
matchId44. 5
)445 6
;446 7
if55 

(55 
	gameState55 
==55 
null55 
)55 
{66 	
	gameState77 
=77 
MatchManager77 $
.77$ %
CreateMatch77% 0
(770 1
matchId771 8
)778 9
;779 :
Console88 
.88 
	WriteLine88 
(88 
$"88  
$str88  <
{88< =
matchId88= D
}88D E
"88E F
)88F G
;88G H
}99 	
MatchPlayerCounts<< 
.<< 
AddOrUpdate<< %
(<<% &
matchId<<& -
,<<- .
$num<</ 0
,<<0 1
(<<2 3
_<<3 4
,<<4 5
count<<6 ;
)<<; <
=><<= ?
count<<@ E
+<<F G
$num<<H I
)<<I J
;<<J K
await?? 
Groups?? 
.?? 
AddToGroupAsync?? $
(??$ %
Context??% ,
.??, -
ConnectionId??- 9
,??9 :
matchId??; B
.??B C
ToString??C K
(??K L
)??L M
)??M N
;??N O
varAA 
playerCountAA 
=AA 
MatchPlayerCountsAA +
.AA+ ,
GetValueOrDefaultAA, =
(AA= >
matchIdAA> E
,AAE F
$numAAG H
)AAH I
;AAI J
ConsoleBB 
.BB 
	WriteLineBB 
(BB 
$"BB 
$strBB -
{BB- .
userIdBB. 4
}BB4 5
$strBB5 I
{BBI J
matchIdBBJ Q
}BBQ R
$strBBR ]
{BB] ^
playerCountBB^ i
}BBi j
$strBBj k
"BBk l
)BBl m
;BBm n
awaitDD 
baseDD 
.DD 
OnConnectedAsyncDD #
(DD# $
)DD$ %
;DD% &
awaitGG 
OnClientConnectedGG 
(GG  
userIdGG  &
,GG& '
matchIdGG( /
)GG/ 0
;GG0 1
}HH 
publicJJ 

overrideJJ 
asyncJJ 
TaskJJ 
OnDisconnectedAsyncJJ 2
(JJ2 3
	ExceptionJJ3 <
?JJ< =
	exceptionJJ> G
)JJG H
{KK 
ifMM 

(MM 
ConnectionsMM 
.MM 
	TryRemoveMM !
(MM! "
ContextMM" )
.MM) *
ConnectionIdMM* 6
,MM6 7
outMM8 ;
varMM< ?

connectionMM@ J
)MMJ K
)MMK L
{NN 	
varOO 
(OO 
userIdOO 
,OO 
matchIdOO  
)OO  !
=OO" #

connectionOO$ .
;OO. /
awaitRR 
GroupsRR 
.RR  
RemoveFromGroupAsyncRR -
(RR- .
ContextRR. 5
.RR5 6
ConnectionIdRR6 B
,RRB C
matchIdRRD K
.RRK L
ToStringRRL T
(RRT U
)RRU V
)RRV W
;RRW X
varUU 
remainingPlayersUU  
=UU! "
MatchPlayerCountsUU# 4
.UU4 5
AddOrUpdateUU5 @
(UU@ A
matchIdUUA H
,UUH I
$numUUJ K
,UUK L
(UUM N
_UUN O
,UUO P
countUUQ V
)UUV W
=>UUX Z
MathUU[ _
.UU_ `
MaxUU` c
(UUc d
$numUUd e
,UUe f
countUUg l
-UUm n
$numUUo p
)UUp q
)UUq r
;UUr s
ConsoleWW 
.WW 
	WriteLineWW 
(WW 
$"WW  
$strWW  1
{WW1 2
userIdWW2 8
}WW8 9
$strWW9 R
{WWR S
matchIdWWS Z
}WWZ [
$strWW[ h
{WWh i
remainingPlayersWWi y
}WWy z
$strWWz {
"WW{ |
)WW| }
;WW} ~
if[[ 
([[ 
remainingPlayers[[  
==[[! #
$num[[$ %
)[[% &
{\\ 
MatchPlayerCounts]] !
.]]! "
	TryRemove]]" +
(]]+ ,
matchId]], 3
,]]3 4
out]]5 8
_]]9 :
)]]: ;
;]]; <
_`` 
=`` 
Task`` 
.`` 
Delay`` 
(`` 
TimeSpan`` '
.``' (
FromSeconds``( 3
(``3 4
$num``4 5
)``5 6
)``6 7
.``7 8
ContinueWith``8 D
(``D E
_``E F
=>``G I
{aa 
MatchManagerbb  
.bb  !
RemoveMatchbb! ,
(bb, -
matchIdbb- 4
)bb4 5
;bb5 6
Consolecc 
.cc 
	WriteLinecc %
(cc% &
$"cc& (
$strcc( 8
{cc8 9
matchIdcc9 @
}cc@ A
$strccA `
"cc` a
)cca b
;ccb c
}dd 
)dd 
;dd 
}ee 
}ff 	
awaithh 
basehh 
.hh 
OnDisconnectedAsynchh &
(hh& '
	exceptionhh' 0
)hh0 1
;hh1 2
}ii 
	protectedss 
virtualss 
asyncss 
Taskss  
OnClientConnectedss! 2
(ss2 3
Guidss3 7
userIdss8 >
,ss> ?
Guidss@ D
matchIdssE L
)ssL M
{tt 
varvv 
	gameStatevv 
=vv 
MatchManagervv $
.vv$ %
GetMatchvv% -
(vv- .
matchIdvv. 5
)vv5 6
;vv6 7
ifww 

(ww 
	gameStateww 
!=ww 
nullww 
&&ww  
	gameStateww! *
isww+ -

TGameStateww. 8

typedStateww9 C
)wwC D
{xx 	
awaityy !
SendStateSyncToClientyy '
(yy' (

typedStateyy( 2
,yy2 3
userIdyy4 :
)yy: ;
;yy; <
}zz 	
}{{ 
	protected
ÅÅ 
virtual
ÅÅ 
async
ÅÅ 
Task
ÅÅ  #
SendStateSyncToClient
ÅÅ! 6
(
ÅÅ6 7

TGameState
ÅÅ7 A
	gameState
ÅÅB K
,
ÅÅK L
Guid
ÅÅM Q
userId
ÅÅR X
)
ÅÅX Y
{
ÇÇ 
var
ÑÑ 

syncAction
ÑÑ 
=
ÑÑ 
new
ÑÑ !
SyncGameStateAction
ÑÑ 0
<
ÑÑ0 1

TGameState
ÑÑ1 ;
>
ÑÑ; <
(
ÑÑ< =
	gameState
ÑÑ= F
)
ÑÑF G
;
ÑÑG H
var
ÖÖ 
json
ÖÖ 
=
ÖÖ 

Newtonsoft
ÖÖ 
.
ÖÖ 
Json
ÖÖ "
.
ÖÖ" #
JsonConvert
ÖÖ# .
.
ÖÖ. /
SerializeObject
ÖÖ/ >
(
ÖÖ> ?
new
ÖÖ? B
[
ÖÖB C
]
ÖÖC D
{
ÖÖE F

syncAction
ÖÖG Q
}
ÖÖR S
)
ÖÖS T
;
ÖÖT U
var
àà 
connectionId
àà 
=
àà 
Connections
àà &
.
àà& '
FirstOrDefault
àà' 5
(
àà5 6
c
àà6 7
=>
àà8 :
c
àà; <
.
àà< =
Value
àà= B
.
ààB C
PlayerId
ààC K
==
ààL N
userId
ààO U
)
ààU V
.
ààV W
Key
ààW Z
;
ààZ [
if
ââ 

(
ââ 
connectionId
ââ 
!=
ââ 
null
ââ  
)
ââ  !
{
ää 	
await
ãã 
Clients
ãã 
.
ãã 
Client
ãã  
(
ãã  !
connectionId
ãã! -
)
ãã- .
.
ãã. /
	SendAsync
ãã/ 8
(
ãã8 9
$str
ãã9 F
,
ããF G
json
ããH L
)
ããL M
;
ããM N
Console
åå 
.
åå 
	WriteLine
åå 
(
åå 
$"
åå  
$str
åå  D
{
ååD E
userId
ååE K
}
ååK L
$str
ååL N
{
ååN O
	gameState
ååO X
.
ååX Y
History
ååY `
.
åå` a
Count
ååa f
}
ååf g
$str
ååg p
"
ååp q
)
ååq r
;
åår s
}
çç 	
}
éé 
	protected
êê 
(
êê 
Guid
êê 
UserId
êê 
,
êê 
Guid
êê  
MatchId
êê! (
)
êê( )
GetConnection
êê* 7
(
êê7 8
)
êê8 9
{
ëë 
return
íí 
Connections
íí 
[
íí 
Context
íí "
.
íí" #
ConnectionId
íí# /
]
íí/ 0
;
íí0 1
}
ìì 
	protected
ïï 
void
ïï 
ScheduleAction
ïï !
(
ïï! "
Action
ïï" (
action
ïï) /
)
ïï/ 0
{
ññ 
ServerDomain
óó 
.
óó 
GameLoop
óó 
.
óó 
Schedule
óó &
(
óó& '
action
óó' -
)
óó- .
;
óó. /
}
òò 
public
¢¢ 

Task
¢¢ 
Ping
¢¢ 
(
¢¢ 
long
¢¢ 

clientTime
¢¢ $
)
¢¢$ %
{
££ 
return
§§ 
Clients
§§ 
.
§§ 
Caller
§§ 
.
§§ 
	SendAsync
§§ '
(
§§' (
$str
§§( .
,
§§. /

clientTime
§§0 :
)
§§: ;
;
§§; <
}
•• 
public
¨¨ 

Task
¨¨ 
TimeSync
¨¨ 
(
¨¨ 
long
¨¨ 
clientTicks
¨¨ )
)
¨¨) *
{
≠≠ 
var
∞∞ 
serverTicks
∞∞ 
=
∞∞ 
DateTime
∞∞ "
.
∞∞" #
UtcNow
∞∞# )
.
∞∞) *
Ticks
∞∞* /
;
∞∞/ 0
return
±± 
Clients
±± 
.
±± 
Caller
±± 
.
±± 
	SendAsync
±± '
(
±±' (
$str
±±( 2
,
±±2 3
serverTicks
±±4 ?
)
±±? @
;
±±@ A
}
≤≤ 
public
ππ 

void
ππ 
SyncActions
ππ 
(
ππ 
string
ππ "
actionsJson
ππ# .
)
ππ. /
{
∫∫ 
var
ªª 
(
ªª 
userId
ªª 
,
ªª 
matchId
ªª 
)
ªª 
=
ªª 
GetConnection
ªª  -
(
ªª- .
)
ªª. /
;
ªª/ 0
Console
ºº 
.
ºº 
	WriteLine
ºº 
(
ºº 
$"
ºº 
$str
ºº A
{
ººA B
userId
ººB H
}
ººH I
$str
ººI X
{
ººX Y
actionsJson
ººY d
?
ººd e
.
ººe f
Length
ººf l
??
ººm o
$num
ººp q
}
ººq r
"
ººr s
)
ººs t
;
ººt u
Console
ΩΩ 
.
ΩΩ 
	WriteLine
ΩΩ 
(
ΩΩ 
$"
ΩΩ 
$str
ΩΩ 4
{
ΩΩ4 5
actionsJson
ΩΩ5 @
?
ΩΩ@ A
.
ΩΩA B
	Substring
ΩΩB K
(
ΩΩK L
$num
ΩΩL M
,
ΩΩM N
Math
ΩΩO S
.
ΩΩS T
Min
ΩΩT W
(
ΩΩW X
$num
ΩΩX [
,
ΩΩ[ \
actionsJson
ΩΩ] h
?
ΩΩh i
.
ΩΩi j
Length
ΩΩj p
??
ΩΩq s
$num
ΩΩt u
)
ΩΩu v
)
ΩΩv w
}
ΩΩw x
"
ΩΩx y
)
ΩΩy z
;
ΩΩz {
if
¿¿ 

(
¿¿ 
ServerDomain
¿¿ 
.
¿¿ 
RelayOnlyMode
¿¿ &
)
¿¿& '
{
¡¡ 	
ScheduleAction
¬¬ 
(
¬¬ 
(
¬¬ 
)
¬¬ 
=>
¬¬  
{
√√ 
Clients
ƒƒ 
.
ƒƒ 
Group
ƒƒ 
(
ƒƒ 
matchId
ƒƒ %
.
ƒƒ% &
ToString
ƒƒ& .
(
ƒƒ. /
)
ƒƒ/ 0
)
ƒƒ0 1
.
ƒƒ1 2
	SendAsync
ƒƒ2 ;
(
ƒƒ; <
$str
ƒƒ< I
,
ƒƒI J
actionsJson
ƒƒK V
)
ƒƒV W
;
ƒƒW X
Console
≈≈ 
.
≈≈ 
	WriteLine
≈≈ !
(
≈≈! "
$"
≈≈" $
$str
≈≈$ H
{
≈≈H I
userId
≈≈I O
}
≈≈O P
$str
≈≈P Z
{
≈≈Z [
matchId
≈≈[ b
}
≈≈b c
"
≈≈c d
)
≈≈d e
;
≈≈e f
}
∆∆ 
)
∆∆ 
;
∆∆ 
return
«« 
;
«« 
}
»» 	
ScheduleAction
ÀÀ 
(
ÀÀ 
(
ÀÀ 
)
ÀÀ 
=>
ÀÀ 
{
ÃÃ 	
var
—— 
	gameState
—— 
=
—— 
MatchManager
—— (
.
——( )
GetMatch
——) 1
(
——1 2
matchId
——2 9
)
——9 :
;
——: ;
if
““ 
(
““ 
	gameState
““ 
==
““ 
null
““ !
)
““! "
{
”” 
Console
‘‘ 
.
‘‘ 
	WriteLine
‘‘ !
(
‘‘! "
$"
‘‘" $
$str
‘‘$ 4
{
‘‘4 5
matchId
‘‘5 <
}
‘‘< =
$str
‘‘= G
"
‘‘G H
)
‘‘H I
;
‘‘I J
return
’’ 
;
’’ 
}
÷÷ 
var
⁄⁄ 
executor
⁄⁄ 
=
⁄⁄ 
new
⁄⁄ #
NetworkActionExecutor
⁄⁄ 4
(
⁄⁄4 5
	gameState
⁄⁄5 >
.
⁄⁄> ?
Registry
⁄⁄? G
)
⁄⁄G H
;
⁄⁄H I
executor
€€ 
.
€€ 
BeforeAction
€€ !
+=
€€" $
action
€€% +
=>
€€, .
action
€€/ 5
.
€€5 6
SyncToClient
€€6 B
=
€€C D
true
€€E I
;
€€I J
var
‹‹ 
count
‹‹ 
=
‹‹ 
executor
‹‹  
.
‹‹  !
ExecuteBatch
‹‹! -
(
‹‹- .
actionsJson
›› 
,
›› 

executorId
ﬁﬁ 
:
ﬁﬁ 
userId
ﬁﬁ "
,
ﬁﬁ" #
onError
ﬂﬂ 
:
ﬂﬂ 
error
ﬂﬂ 
=>
ﬂﬂ !
Console
ﬂﬂ" )
.
ﬂﬂ) *
	WriteLine
ﬂﬂ* 3
(
ﬂﬂ3 4
$"
ﬂﬂ4 6
$str
ﬂﬂ6 @
{
ﬂﬂ@ A
error
ﬂﬂA F
}
ﬂﬂF G
"
ﬂﬂG H
)
ﬂﬂH I
)
‡‡ 
;
‡‡ 
if
‚‚ 
(
‚‚ 
count
‚‚ 
>
‚‚ 
$num
‚‚ 
)
‚‚ 
{
„„ 
Console
‰‰ 
.
‰‰ 
	WriteLine
‰‰ !
(
‰‰! "
$"
‰‰" $
$str
‰‰$ 7
{
‰‰7 8
count
‰‰8 =
}
‰‰= >
$str
‰‰> S
{
‰‰S T
userId
‰‰T Z
}
‰‰Z [
"
‰‰[ \
)
‰‰\ ]
;
‰‰] ^
}
ÂÂ 
}
ÊÊ 	
)
ÊÊ	 

;
ÊÊ
 
}
ÁÁ 
}ËË Â
t/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/HistoryDomain.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class 
HistoryDomain 
: 
BranchDomain )
{		 
public

 

List

 
<

 
INetworkAction

 
>

 
History

  '
{

( )
get

* -
;

- .
}

/ 0
=

1 2
new

3 6
(

6 7
)

7 8
;

8 9
public 

HistoryDomain 
( 
BranchDomain %
parent& ,
), -
:. /
base0 4
(4 5
parent5 ;
); <
{ 
new 
Reaction 
< 

LeafDomain 
,  
INetworkAction! /
>/ 0
(0 1
parent1 7
)7 8
. 
After 
( 
( 
_ 
, 
action 
) 
=> !
History" )
.) *
Add* -
(- .
action. 4
)4 5
)5 6
. 
AddTo 
( 
Disposables 
) 
;  
} 
public 

void 
Clear 
( 
) 
=> 
History "
." #
Clear# (
(( )
)) *
;* +
} èË
{/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/DeterminismValidator.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class  
DeterminismValidator !
<! "

TGameState" ,
>, -
where. 3

TGameState4 >
:? @
NetworkGameStateA Q
{ 
public 

static 
bool 
	IsEnabled  
{! "
get# &
;& '
set( +
;+ ,
}- .
=/ 0
false1 6
;6 7
public 

static 
bool "
UseFastStateComparison -
{. /
get0 3
;3 4
set5 8
;8 9
}: ;
=< =
true> B
;B C
public%% 

static%% 
bool%% 
SkipStateValidation%% *
{%%+ ,
get%%- 0
;%%0 1
set%%2 5
;%%5 6
}%%7 8
=%%9 :
true%%; ?
;%%? @
public++ 

static++ 
event++ 
Action++ 
<++ 
Guid++ #
,++# $
string++% +
,+++ ,
string++- 3
,++3 4
string++5 ;
,++; <
string++= C
,++C D
int++E H
>++H I
?++I J 
OnDeterminismFailure++K _
;++_ `
private-- 
readonly-- 

TGameState-- 
_primary--  (
;--( )
private.. 
readonly.. 

TGameState.. 
_shadow..  '
;..' (
private// 
readonly// 
Guid// 
_matchId// "
;//" #
private00 
readonly00 !
NetworkActionExecutor00 *
_shadowExecutor00+ :
;00: ;
private11 
int11 
_actionCount11 
=11 
$num11  
;11  !
private22 
bool22 

_hasFailed22 
=22 
false22 #
;22# $
public44 


TGameState44 
Primary44 
=>44  
_primary44! )
;44) *
public55 


TGameState55 
Shadow55 
=>55 
_shadow55  '
;55' (
public66 

bool66 
	HasFailed66 
=>66 

_hasFailed66 '
;66' (
public77 

int77 
ActionCount77 
=>77 
_actionCount77 *
;77* +
public== 
 
DeterminismValidator== 
(==  

TGameState==  *
primary==+ 2
,==2 3

TGameState==4 >
shadow==? E
,==E F
Guid==G K
matchId==L S
)==S T
{>> 
_primary?? 
=?? 
primary?? 
;?? 
_shadow@@ 
=@@ 
shadow@@ 
;@@ 
_matchIdAA 
=AA 
matchIdAA 
;AA 
_shadowExecutorBB 
=BB 
newBB !
NetworkActionExecutorBB 3
(BB3 4
_shadowBB4 ;
.BB; <
RegistryBB< D
)BBD E
;BBE F
ConsoleDD 
.DD 
	WriteLineDD 
(DD 
$"DD 
$strDD I
{DDI J
matchIdDDJ Q
}DDQ R
"DDR S
)DDS T
;DDT U
}EE 
publicNN 

boolNN 
ValidateActionNN 
(NN 
INetworkActionNN -
actionNN. 4
,NN4 5
ActionNN6 <
executePrimaryNN= K
)NNK L
{OO 
ifPP 

(PP 

_hasFailedPP 
)PP 
{QQ 	
executePrimaryRR 
(RR 
)RR 
;RR 
returnSS 
trueSS 
;SS 
}TT 	
_actionCountVV 
++VV 
;VV 
tryXX 
{YY 	
var[[ 

actionJson[[ 
=[[ 
JsonSerializer[[ +
.[[+ ,
ToJson[[, 2
([[2 3
action[[3 9
)[[9 :
;[[: ;
var\\ 
clonedAction\\ 
=\\ 
JsonSerializer\\ -
.\\- .
FromJson\\. 6
<\\6 7
INetworkAction\\7 E
>\\E F
(\\F G

actionJson\\G Q
)\\Q R
;\\R S
if^^ 
(^^ 
clonedAction^^ 
==^^ 
null^^  $
)^^$ %
{__ 
Console`` 
.`` 
	WriteLine`` !
(``! "
$"``" $
$str``$ [
{``[ \
action``\ b
.``b c
GetType``c j
(``j k
)``k l
.``l m
Name``m q
}``q r
"``r s
)``s t
;``t u
executePrimaryaa 
(aa 
)aa  
;aa  !
returnbb 
truebb 
;bb 
}cc 
varff 
shadowLoggerff 
=ff 
ExecutionLoggerff .
.ff. /
BeginLoggingff/ ;
(ff; <
)ff< =
;ff= >
_shadowExecutorgg 
.gg 
ExecuteActiongg )
(gg) *
clonedActiongg* 6
,gg6 7
actiongg8 >
.gg> ?

ExecutorIdgg? I
,ggI J
errorhh 
=>hh 
Consolehh  
.hh  !
	WriteLinehh! *
(hh* +
$"hh+ -
$strhh- \
{hh\ ]
errorhh] b
}hhb c
"hhc d
)hhd e
)hhe f
;hhf g
varii 

shadowLogsii 
=ii 
shadowLoggerii )
.ii) *
Logsii* .
.ii. /
ToListii/ 5
(ii5 6
)ii6 7
;ii7 8
ExecutionLoggerjj 
.jj 

EndLoggingjj &
(jj& '
)jj' (
;jj( )
varmm 
primaryLoggermm 
=mm 
ExecutionLoggermm  /
.mm/ 0
BeginLoggingmm0 <
(mm< =
)mm= >
;mm> ?
executePrimarynn 
(nn 
)nn 
;nn 
varoo 
primaryLogsoo 
=oo 
primaryLoggeroo +
.oo+ ,
Logsoo, 0
.oo0 1
ToListoo1 7
(oo7 8
)oo8 9
;oo9 :
ExecutionLoggerpp 
.pp 

EndLoggingpp &
(pp& '
)pp' (
;pp( )
Consoless 
.ss 
	WriteLiness 
(ss 
$"ss  
$strss  O
{ssO P
primaryLogsssP [
.ss[ \
Countss\ a
}ssa b
$strssb r
{ssr s

shadowLogssss }
.ss} ~
Count	ss~ É
}
ssÉ Ñ
$str
ssÑ ã
"
ssã å
)
sså ç
;
ssç é
vartt 
mismatchIndextt 
=tt  
CompareExecutionLogstt  4
(tt4 5
primaryLogstt5 @
,tt@ A

shadowLogsttB L
)ttL M
;ttM N
ifuu 
(uu 
mismatchIndexuu 
>=uu  
$numuu! "
)uu" #
{vv #
LogEventSequenceFailureww '
(ww' (
actionww( .
,ww. /

actionJsonww0 :
,ww: ;
primaryLogsww< G
,wwG H

shadowLogswwI S
,wwS T
mismatchIndexwwU b
)wwb c
;wwc d

_hasFailedxx 
=xx 
truexx !
;xx! "
returnyy 
falseyy 
;yy 
}zz 
else{{ 
{|| 
Console}} 
.}} 
	WriteLine}} !
(}}! "
$"}}" $
$str}}$ c
"}}c d
)}}d e
;}}e f
}~~ 
if
ÅÅ 
(
ÅÅ 
!
ÅÅ !
SkipStateValidation
ÅÅ $
)
ÅÅ$ %
{
ÇÇ 
if
ÉÉ 
(
ÉÉ $
UseFastStateComparison
ÉÉ *
)
ÉÉ* +
{
ÑÑ 
var
ÜÜ 
primaryHash
ÜÜ #
=
ÜÜ$ %
GetStateHash
ÜÜ& 2
(
ÜÜ2 3
_primary
ÜÜ3 ;
)
ÜÜ; <
;
ÜÜ< =
var
áá 

shadowHash
áá "
=
áá# $
GetStateHash
áá% 1
(
áá1 2
_shadow
áá2 9
)
áá9 :
;
áá: ;
if
ââ 
(
ââ 
primaryHash
ââ #
!=
ââ$ &

shadowHash
ââ' 1
)
ââ1 2
{
ää 
var
åå 
primaryStateJson
åå ,
=
åå- .
JsonSerializer
åå/ =
.
åå= >
ToJson
åå> D
(
ååD E
_primary
ååE M
)
ååM N
;
ååN O
var
çç 
shadowStateJson
çç +
=
çç, -
JsonSerializer
çç. <
.
çç< =
ToJson
çç= C
(
ççC D
_shadow
ççD K
)
ççK L
;
ççL M%
LogStateSnapshotFailure
éé /
(
éé/ 0
action
éé0 6
,
éé6 7

actionJson
éé8 B
,
ééB C
primaryStateJson
ééD T
,
ééT U
shadowStateJson
ééV e
)
éée f
;
ééf g

_hasFailed
èè "
=
èè# $
true
èè% )
;
èè) *
return
êê 
false
êê $
;
êê$ %
}
ëë 
}
íí 
else
ìì 
{
îî 
var
ññ 
primaryStateJson
ññ (
=
ññ) *
JsonSerializer
ññ+ 9
.
ññ9 :
ToJson
ññ: @
(
ññ@ A
_primary
ññA I
)
ññI J
;
ññJ K
var
óó 
shadowStateJson
óó '
=
óó( )
JsonSerializer
óó* 8
.
óó8 9
ToJson
óó9 ?
(
óó? @
_shadow
óó@ G
)
óóG H
;
óóH I
if
ôô 
(
ôô 
primaryStateJson
ôô (
!=
ôô) +
shadowStateJson
ôô, ;
)
ôô; <
{
öö %
LogStateSnapshotFailure
õõ /
(
õõ/ 0
action
õõ0 6
,
õõ6 7

actionJson
õõ8 B
,
õõB C
primaryStateJson
õõD T
,
õõT U
shadowStateJson
õõV e
)
õõe f
;
õõf g

_hasFailed
úú "
=
úú# $
true
úú% )
;
úú) *
return
ùù 
false
ùù $
;
ùù$ %
}
ûû 
}
üü 
Console
°° 
.
°° 
	WriteLine
°° !
(
°°! "
$"
°°" $
$str
°°$ d
"
°°d e
)
°°e f
;
°°f g
}
¢¢ 
else
££ 
{
§§ 
Console
•• 
.
•• 
	WriteLine
•• !
(
••! "
$"
••" $
$str
••$ g
"
••g h
)
••h i
;
••i j
}
¶¶ 
if
©© 
(
©© 
_actionCount
©© 
%
©© 
$num
©© "
==
©©# %
$num
©©& '
)
©©' (
{
™™ 
Console
´´ 
.
´´ 
	WriteLine
´´ !
(
´´! "
$"
´´" $
$str
´´$ A
{
´´A B
_matchId
´´B J
}
´´J K
$str
´´K M
{
´´M N
_actionCount
´´N Z
}
´´Z [
$str
´´[ o
{
´´o p
primaryLogs
´´p {
.
´´{ |
Count´´| Å
}´´Å Ç
$str´´Ç ì
"´´ì î
)´´î ï
;´´ï ñ
}
¨¨ 
return
ÆÆ 
true
ÆÆ 
;
ÆÆ 
}
ØØ 	
catch
∞∞ 
(
∞∞ 
	Exception
∞∞ 
ex
∞∞ 
)
∞∞ 
{
±± 	
Console
≤≤ 
.
≤≤ 
	WriteLine
≤≤ 
(
≤≤ 
$"
≤≤  
$str
≤≤  T
{
≤≤T U
ex
≤≤U W
.
≤≤W X
Message
≤≤X _
}
≤≤_ `
"
≤≤` a
)
≤≤a b
;
≤≤b c
Console
≥≥ 
.
≥≥ 
	WriteLine
≥≥ 
(
≥≥ 
ex
≥≥  
.
≥≥  !

StackTrace
≥≥! +
)
≥≥+ ,
;
≥≥, -

_hasFailed
¥¥ 
=
¥¥ 
true
¥¥ 
;
¥¥ 
return
µµ 
false
µµ 
;
µµ 
}
∂∂ 	
}
∑∑ 
private
ºº 
int
ºº "
CompareExecutionLogs
ºº $
(
ºº$ %
List
ºº% )
<
ºº) *
ExecutionLog
ºº* 6
>
ºº6 7
primaryLogs
ºº8 C
,
ººC D
List
ººE I
<
ººI J
ExecutionLog
ººJ V
>
ººV W

shadowLogs
ººX b
)
ººb c
{
ΩΩ 
var
ææ 
maxCount
ææ 
=
ææ 
Math
ææ 
.
ææ 
Max
ææ 
(
ææ  
primaryLogs
ææ  +
.
ææ+ ,
Count
ææ, 1
,
ææ1 2

shadowLogs
ææ3 =
.
ææ= >
Count
ææ> C
)
ææC D
;
ææD E
for
¿¿ 
(
¿¿ 
int
¿¿ 
i
¿¿ 
=
¿¿ 
$num
¿¿ 
;
¿¿ 
i
¿¿ 
<
¿¿ 
maxCount
¿¿ $
;
¿¿$ %
i
¿¿& '
++
¿¿' )
)
¿¿) *
{
¡¡ 	
if
¬¬ 
(
¬¬ 
i
¬¬ 
>=
¬¬ 
primaryLogs
¬¬  
.
¬¬  !
Count
¬¬! &
)
¬¬& '
{
√√ 
return
ƒƒ 
i
ƒƒ 
;
ƒƒ 
}
≈≈ 
if
∆∆ 
(
∆∆ 
i
∆∆ 
>=
∆∆ 

shadowLogs
∆∆ 
.
∆∆  
Count
∆∆  %
)
∆∆% &
{
«« 
return
»» 
i
»» 
;
»» 
}
…… 
if
ÀÀ 
(
ÀÀ 
!
ÀÀ 
primaryLogs
ÀÀ 
[
ÀÀ 
i
ÀÀ 
]
ÀÀ 
.
ÀÀ  
Matches
ÀÀ  '
(
ÀÀ' (

shadowLogs
ÀÀ( 2
[
ÀÀ2 3
i
ÀÀ3 4
]
ÀÀ4 5
)
ÀÀ5 6
)
ÀÀ6 7
{
ÃÃ 
return
ÕÕ 
i
ÕÕ 
;
ÕÕ 
}
ŒŒ 
}
œœ 	
return
—— 
-
—— 
$num
—— 
;
—— 
}
““ 
private
‘‘ 
void
‘‘ %
LogEventSequenceFailure
‘‘ (
(
‘‘( )
INetworkAction
‘‘) 7
action
‘‘8 >
,
‘‘> ?
string
‘‘@ F

actionJson
‘‘G Q
,
‘‘Q R
List
’’ 
<
’’ 
ExecutionLog
’’ 
>
’’ 
primaryLogs
’’ &
,
’’& '
List
’’( ,
<
’’, -
ExecutionLog
’’- 9
>
’’9 :

shadowLogs
’’; E
,
’’E F
int
’’G J
mismatchIndex
’’K X
)
’’X Y
{
÷÷ 
var
◊◊ 

actionType
◊◊ 
=
◊◊ 
action
◊◊ 
.
◊◊  
GetType
◊◊  '
(
◊◊' (
)
◊◊( )
.
◊◊) *
Name
◊◊* .
;
◊◊. /
Console
ŸŸ 
.
ŸŸ 
	WriteLine
ŸŸ 
(
ŸŸ 
$str
ŸŸ \
)
ŸŸ\ ]
;
ŸŸ] ^
Console
⁄⁄ 
.
⁄⁄ 
	WriteLine
⁄⁄ 
(
⁄⁄ 
$str
⁄⁄ \
)
⁄⁄\ ]
;
⁄⁄] ^
Console
€€ 
.
€€ 
	WriteLine
€€ 
(
€€ 
$str
€€ \
)
€€\ ]
;
€€] ^
Console
‹‹ 
.
‹‹ 
	WriteLine
‹‹ 
(
‹‹ 
$"
‹‹ 
$str
‹‹ *
{
‹‹* +
_matchId
‹‹+ 3
}
‹‹3 4
"
‹‹4 5
)
‹‹5 6
;
‹‹6 7
Console
›› 
.
›› 
	WriteLine
›› 
(
›› 
$"
›› 
$str
›› *
{
››* +
_actionCount
››+ 7
}
››7 8
"
››8 9
)
››9 :
;
››: ;
Console
ﬁﬁ 
.
ﬁﬁ 
	WriteLine
ﬁﬁ 
(
ﬁﬁ 
$"
ﬁﬁ 
$str
ﬁﬁ *
{
ﬁﬁ* +

actionType
ﬁﬁ+ 5
}
ﬁﬁ5 6
"
ﬁﬁ6 7
)
ﬁﬁ7 8
;
ﬁﬁ8 9
Console
ﬂﬂ 
.
ﬂﬂ 
	WriteLine
ﬂﬂ 
(
ﬂﬂ 
$"
ﬂﬂ 
$str
ﬂﬂ 1
{
ﬂﬂ1 2
mismatchIndex
ﬂﬂ2 ?
}
ﬂﬂ? @
"
ﬂﬂ@ A
)
ﬂﬂA B
;
ﬂﬂB C
Console
‡‡ 
.
‡‡ 
	WriteLine
‡‡ 
(
‡‡ 
$str
‡‡ \
)
‡‡\ ]
;
‡‡] ^
var
„„ 
contextStart
„„ 
=
„„ 
Math
„„ 
.
„„  
Max
„„  #
(
„„# $
$num
„„$ %
,
„„% &
mismatchIndex
„„' 4
-
„„5 6
$num
„„7 8
)
„„8 9
;
„„9 :
var
‰‰ 

contextEnd
‰‰ 
=
‰‰ 
Math
‰‰ 
.
‰‰ 
Min
‰‰ !
(
‰‰! "
Math
‰‰" &
.
‰‰& '
Max
‰‰' *
(
‰‰* +
primaryLogs
‰‰+ 6
.
‰‰6 7
Count
‰‰7 <
,
‰‰< =

shadowLogs
‰‰> H
.
‰‰H I
Count
‰‰I N
)
‰‰N O
,
‰‰O P
mismatchIndex
‰‰Q ^
+
‰‰_ `
$num
‰‰a b
)
‰‰b c
;
‰‰c d
Console
ÊÊ 
.
ÊÊ 
	WriteLine
ÊÊ 
(
ÊÊ 
$str
ÊÊ 0
)
ÊÊ0 1
;
ÊÊ1 2
for
ÁÁ 
(
ÁÁ 
int
ÁÁ 
i
ÁÁ 
=
ÁÁ 
contextStart
ÁÁ !
;
ÁÁ! "
i
ÁÁ# $
<
ÁÁ% &

contextEnd
ÁÁ' 1
&&
ÁÁ2 4
i
ÁÁ5 6
<
ÁÁ7 8
primaryLogs
ÁÁ9 D
.
ÁÁD E
Count
ÁÁE J
;
ÁÁJ K
i
ÁÁL M
++
ÁÁM O
)
ÁÁO P
{
ËË 	
var
ÈÈ 
marker
ÈÈ 
=
ÈÈ 
i
ÈÈ 
==
ÈÈ 
mismatchIndex
ÈÈ +
?
ÈÈ, -
$str
ÈÈ. 4
:
ÈÈ5 6
$str
ÈÈ7 =
;
ÈÈ= >
Console
ÍÍ 
.
ÍÍ 
	WriteLine
ÍÍ 
(
ÍÍ 
$"
ÍÍ  
$str
ÍÍ  "
{
ÍÍ" #
marker
ÍÍ# )
}
ÍÍ) *
{
ÍÍ* +
primaryLogs
ÍÍ+ 6
[
ÍÍ6 7
i
ÍÍ7 8
]
ÍÍ8 9
}
ÍÍ9 :
"
ÍÍ: ;
)
ÍÍ; <
;
ÍÍ< =
}
ÎÎ 	
if
ÏÏ 

(
ÏÏ 
mismatchIndex
ÏÏ 
>=
ÏÏ 
primaryLogs
ÏÏ (
.
ÏÏ( )
Count
ÏÏ) .
)
ÏÏ. /
{
ÌÌ 	
Console
ÓÓ 
.
ÓÓ 
	WriteLine
ÓÓ 
(
ÓÓ 
$"
ÓÓ  
$str
ÓÓ  6
{
ÓÓ6 7
mismatchIndex
ÓÓ7 D
}
ÓÓD E
$str
ÓÓE F
"
ÓÓF G
)
ÓÓG H
;
ÓÓH I
}
ÔÔ 	
Console
ÒÒ 
.
ÒÒ 
	WriteLine
ÒÒ 
(
ÒÒ 
$str
ÒÒ 
)
ÒÒ 
;
ÒÒ 
Console
ÚÚ 
.
ÚÚ 
	WriteLine
ÚÚ 
(
ÚÚ 
$str
ÚÚ /
)
ÚÚ/ 0
;
ÚÚ0 1
for
ÛÛ 
(
ÛÛ 
int
ÛÛ 
i
ÛÛ 
=
ÛÛ 
contextStart
ÛÛ !
;
ÛÛ! "
i
ÛÛ# $
<
ÛÛ% &

contextEnd
ÛÛ' 1
&&
ÛÛ2 4
i
ÛÛ5 6
<
ÛÛ7 8

shadowLogs
ÛÛ9 C
.
ÛÛC D
Count
ÛÛD I
;
ÛÛI J
i
ÛÛK L
++
ÛÛL N
)
ÛÛN O
{
ÙÙ 	
var
ıı 
marker
ıı 
=
ıı 
i
ıı 
==
ıı 
mismatchIndex
ıı +
?
ıı, -
$str
ıı. 4
:
ıı5 6
$str
ıı7 =
;
ıı= >
Console
ˆˆ 
.
ˆˆ 
	WriteLine
ˆˆ 
(
ˆˆ 
$"
ˆˆ  
$str
ˆˆ  "
{
ˆˆ" #
marker
ˆˆ# )
}
ˆˆ) *
{
ˆˆ* +

shadowLogs
ˆˆ+ 5
[
ˆˆ5 6
i
ˆˆ6 7
]
ˆˆ7 8
}
ˆˆ8 9
"
ˆˆ9 :
)
ˆˆ: ;
;
ˆˆ; <
}
˜˜ 	
if
¯¯ 

(
¯¯ 
mismatchIndex
¯¯ 
>=
¯¯ 

shadowLogs
¯¯ '
.
¯¯' (
Count
¯¯( -
)
¯¯- .
{
˘˘ 	
Console
˙˙ 
.
˙˙ 
	WriteLine
˙˙ 
(
˙˙ 
$"
˙˙  
$str
˙˙  6
{
˙˙6 7
mismatchIndex
˙˙7 D
}
˙˙D E
$str
˙˙E F
"
˙˙F G
)
˙˙G H
;
˙˙H I
}
˚˚ 	
Console
˝˝ 
.
˝˝ 
	WriteLine
˝˝ 
(
˝˝ 
$str
˝˝ \
)
˝˝\ ]
;
˝˝] ^
var
ÄÄ 
primaryLogStr
ÄÄ 
=
ÄÄ 
string
ÄÄ "
.
ÄÄ" #
Join
ÄÄ# '
(
ÄÄ' (
$str
ÄÄ( ,
,
ÄÄ, -
primaryLogs
ÄÄ. 9
.
ÄÄ9 :
Select
ÄÄ: @
(
ÄÄ@ A
l
ÄÄA B
=>
ÄÄC E
l
ÄÄF G
.
ÄÄG H
ToString
ÄÄH P
(
ÄÄP Q
)
ÄÄQ R
)
ÄÄR S
)
ÄÄS T
;
ÄÄT U
var
ÅÅ 
shadowLogStr
ÅÅ 
=
ÅÅ 
string
ÅÅ !
.
ÅÅ! "
Join
ÅÅ" &
(
ÅÅ& '
$str
ÅÅ' +
,
ÅÅ+ ,

shadowLogs
ÅÅ- 7
.
ÅÅ7 8
Select
ÅÅ8 >
(
ÅÅ> ?
l
ÅÅ? @
=>
ÅÅA C
l
ÅÅD E
.
ÅÅE F
ToString
ÅÅF N
(
ÅÅN O
)
ÅÅO P
)
ÅÅP Q
)
ÅÅQ R
;
ÅÅR S"
OnDeterminismFailure
ÑÑ 
?
ÑÑ 
.
ÑÑ 
Invoke
ÑÑ $
(
ÑÑ$ %
_matchId
ÑÑ% -
,
ÑÑ- .

actionType
ÑÑ/ 9
,
ÑÑ9 :

actionJson
ÑÑ; E
,
ÑÑE F
primaryLogStr
ÑÑG T
,
ÑÑT U
shadowLogStr
ÑÑV b
,
ÑÑb c
mismatchIndex
ÑÑd q
)
ÑÑq r
;
ÑÑr s
}
ÖÖ 
private
áá 
void
áá %
LogStateSnapshotFailure
áá (
(
áá( )
INetworkAction
áá) 7
action
áá8 >
,
áá> ?
string
áá@ F

actionJson
ááG Q
,
ááQ R
string
àà 
primaryStateJson
àà 
,
àà  
string
àà! '
shadowStateJson
àà( 7
)
àà7 8
{
ââ 
var
ää 

actionType
ää 
=
ää 
action
ää 
.
ää  
GetType
ää  '
(
ää' (
)
ää( )
.
ää) *
Name
ää* .
;
ää. /
Console
åå 
.
åå 
	WriteLine
åå 
(
åå 
$str
åå \
)
åå\ ]
;
åå] ^
Console
çç 
.
çç 
	WriteLine
çç 
(
çç 
$str
çç \
)
çç\ ]
;
çç] ^
Console
éé 
.
éé 
	WriteLine
éé 
(
éé 
$str
éé \
)
éé\ ]
;
éé] ^
Console
èè 
.
èè 
	WriteLine
èè 
(
èè 
$"
èè 
$str
èè *
{
èè* +
_matchId
èè+ 3
}
èè3 4
"
èè4 5
)
èè5 6
;
èè6 7
Console
êê 
.
êê 
	WriteLine
êê 
(
êê 
$"
êê 
$str
êê *
{
êê* +
_actionCount
êê+ 7
}
êê7 8
"
êê8 9
)
êê9 :
;
êê: ;
Console
ëë 
.
ëë 
	WriteLine
ëë 
(
ëë 
$"
ëë 
$str
ëë *
{
ëë* +

actionType
ëë+ 5
}
ëë5 6
"
ëë6 7
)
ëë7 8
;
ëë8 9
Console
íí 
.
íí 
	WriteLine
íí 
(
íí 
$str
íí \
)
íí\ ]
;
íí] ^
Console
ìì 
.
ìì 
	WriteLine
ìì 
(
ìì 
$str
ìì J
)
ììJ K
;
ììK L
Console
îî 
.
îî 
	WriteLine
îî 
(
îî 
$str
îî N
)
îîN O
;
îîO P
Console
ïï 
.
ïï 
	WriteLine
ïï 
(
ïï 
$str
ïï \
)
ïï\ ]
;
ïï] ^
var
òò 
	diffIndex
òò 
=
òò !
FindFirstDifference
òò +
(
òò+ ,
primaryStateJson
òò, <
,
òò< =
shadowStateJson
òò> M
)
òòM N
;
òòN O
var
ôô 
contextStart
ôô 
=
ôô 
Math
ôô 
.
ôô  
Max
ôô  #
(
ôô# $
$num
ôô$ %
,
ôô% &
	diffIndex
ôô' 0
-
ôô1 2
$num
ôô3 5
)
ôô5 6
;
ôô6 7
var
öö 

contextEnd
öö 
=
öö 
Math
öö 
.
öö 
Min
öö !
(
öö! "
primaryStateJson
öö" 2
.
öö2 3
Length
öö3 9
,
öö9 :
	diffIndex
öö; D
+
ööE F
$num
ööG I
)
ööI J
;
ööJ K
Console
úú 
.
úú 
	WriteLine
úú 
(
úú 
$str
úú @
)
úú@ A
;
úúA B
var
ùù 
primaryContext
ùù 
=
ùù 
primaryStateJson
ùù -
.
ùù- .
	Substring
ùù. 7
(
ùù7 8
contextStart
ùù8 D
,
ùùD E
Math
ùùF J
.
ùùJ K
Min
ùùK N
(
ùùN O
$num
ùùO R
,
ùùR S
primaryStateJson
ùùT d
.
ùùd e
Length
ùùe k
-
ùùl m
contextStart
ùùn z
)
ùùz {
)
ùù{ |
;
ùù| }
Console
ûû 
.
ûû 
	WriteLine
ûû 
(
ûû 
$"
ûû 
$str
ûû !
{
ûû! "
primaryContext
ûû" 0
}
ûû0 1
$str
ûû1 4
"
ûû4 5
)
ûû5 6
;
ûû6 7
Console
†† 
.
†† 
	WriteLine
†† 
(
†† 
$str
†† 
)
†† 
;
†† 
Console
°° 
.
°° 
	WriteLine
°° 
(
°° 
$str
°° ?
)
°°? @
;
°°@ A
var
¢¢ 
shadowContext
¢¢ 
=
¢¢ 
shadowStateJson
¢¢ +
.
¢¢+ ,
	Substring
¢¢, 5
(
¢¢5 6
contextStart
¢¢6 B
,
¢¢B C
Math
¢¢D H
.
¢¢H I
Min
¢¢I L
(
¢¢L M
$num
¢¢M P
,
¢¢P Q
shadowStateJson
¢¢R a
.
¢¢a b
Length
¢¢b h
-
¢¢i j
contextStart
¢¢k w
)
¢¢w x
)
¢¢x y
;
¢¢y z
Console
££ 
.
££ 
	WriteLine
££ 
(
££ 
$"
££ 
$str
££ !
{
££! "
shadowContext
££" /
}
££/ 0
$str
££0 3
"
££3 4
)
££4 5
;
££5 6
Console
•• 
.
•• 
	WriteLine
•• 
(
•• 
$str
•• \
)
••\ ]
;
••] ^"
OnDeterminismFailure
®® 
?
®® 
.
®® 
Invoke
®® $
(
®®$ %
_matchId
®®% -
,
®®- .

actionType
®®/ 9
,
®®9 :

actionJson
®®; E
,
®®E F
primaryStateJson
®®G W
,
®®W X
shadowStateJson
®®Y h
,
®®h i
	diffIndex
®®j s
)
®®s t
;
®®t u
}
©© 
private
´´ 
int
´´ !
FindFirstDifference
´´ #
(
´´# $
string
´´$ *
str1
´´+ /
,
´´/ 0
string
´´1 7
str2
´´8 <
)
´´< =
{
¨¨ 
var
≠≠ 
	minLength
≠≠ 
=
≠≠ 
Math
≠≠ 
.
≠≠ 
Min
≠≠  
(
≠≠  !
str1
≠≠! %
.
≠≠% &
Length
≠≠& ,
,
≠≠, -
str2
≠≠. 2
.
≠≠2 3
Length
≠≠3 9
)
≠≠9 :
;
≠≠: ;
for
ÆÆ 
(
ÆÆ 
int
ÆÆ 
i
ÆÆ 
=
ÆÆ 
$num
ÆÆ 
;
ÆÆ 
i
ÆÆ 
<
ÆÆ 
	minLength
ÆÆ %
;
ÆÆ% &
i
ÆÆ' (
++
ÆÆ( *
)
ÆÆ* +
{
ØØ 	
if
∞∞ 
(
∞∞ 
str1
∞∞ 
[
∞∞ 
i
∞∞ 
]
∞∞ 
!=
∞∞ 
str2
∞∞ 
[
∞∞  
i
∞∞  !
]
∞∞! "
)
∞∞" #
{
±± 
return
≤≤ 
i
≤≤ 
;
≤≤ 
}
≥≥ 
}
¥¥ 	
return
µµ 
	minLength
µµ 
;
µµ 
}
∂∂ 
private
∏∏ 
string
∏∏ 
GetStateHash
∏∏ 
(
∏∏  

TGameState
∏∏  *
state
∏∏+ 0
)
∏∏0 1
{
ππ 
var
∫∫ 
json
∫∫ 
=
∫∫ 
JsonSerializer
∫∫ !
.
∫∫! "
ToJson
∫∫" (
(
∫∫( )
state
∫∫) .
)
∫∫. /
;
∫∫/ 0
using
ªª 
var
ªª 
sha256
ªª 
=
ªª 
SHA256
ªª !
.
ªª! "
Create
ªª" (
(
ªª( )
)
ªª) *
;
ªª* +
var
ºº 
	hashBytes
ºº 
=
ºº 
sha256
ºº 
.
ºº 
ComputeHash
ºº *
(
ºº* +
Encoding
ºº+ 3
.
ºº3 4
UTF8
ºº4 8
.
ºº8 9
GetBytes
ºº9 A
(
ººA B
json
ººB F
)
ººF G
)
ººG H
;
ººH I
return
ΩΩ 
Convert
ΩΩ 
.
ΩΩ 
ToHexString
ΩΩ "
(
ΩΩ" #
	hashBytes
ΩΩ# ,
)
ΩΩ, -
;
ΩΩ- .
}
ææ 
}øø ı
â/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/CollectiveActions/WaitForAllAction.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
., -
CollectiveActions- >
;> ?
public 
abstract 
class 
WaitForAllAction &
<& '
TDomain' .
,. /
TAction0 7
>7 8
:9 :
NetworkAction; H
<H I
TDomainI P
,P Q
TActionR Y
>Y Z
,Z [
IWaitForAllAction\ m
,m n,
IRequireCollectiveActionManager	o é
where 	
TDomain
 
: 
class 
, 
IDomain "
where 	
TAction
 
: 
class 
, 
INetworkAction )
{		 
public

 

virtual

 
string

 
CollectiveKey

 '
=>

( *
GetType

+ 2
(

2 3
)

3 4
.

4 5
Name

5 9
;

9 :
[ 

JsonIgnore 
] 
public #
CollectiveActionManager /
?/ 0#
CollectiveActionManager1 H
{I J
getK N
;N O
setP S
;S T
}U V
[ 

JsonIgnore 
] 
public 
bool !
IsCollectiveExecution 2
{3 4
get5 8
;8 9
set: =
;= >
}? @
	protected 
sealed 
override 
void "
ExecuteProcess# 1
(1 2
TDomain2 9
domain: @
)@ A
{ 
if 

( #
CollectiveActionManager #
==$ &
null' +
)+ ,
{ 	
Console 
. 
	WriteLine 
( 
$"  
$str  [
{[ \
GetType\ c
(c d
)d e
.e f
Namef j
}j k
"k l
)l m
;m n
return 
; 
} 	
if 

( #
CollectiveActionManager #
.# $
SubmitWaitForAll$ 4
(4 5
this5 9
)9 :
): ;
{ 	!
IsCollectiveExecution !
=" #
true$ (
;( )
ExecuteWhenReady 
( 
domain #
)# $
;$ %
} 	
else 
{ 	
OnPlayerSubmitted 
( 
domain $
)$ %
;% &
} 	
}   
	protected"" 
abstract"" 
void"" 
ExecuteWhenReady"" ,
("", -
TDomain""- 4
domain""5 ;
)""; <
;""< =
	protected$$ 
virtual$$ 
void$$ 
OnPlayerSubmitted$$ ,
($$, -
TDomain$$- 4
domain$$5 ;
)$$; <
{%% 
Console&& 
.&& 
	WriteLine&& 
(&& 
$"&& 
$str&& 6
{&&6 7

ExecutorId&&7 A
}&&A B
$str&&B M
{&&M N
GetType&&N U
(&&U V
)&&V W
.&&W X
Name&&X \
}&&\ ]
"&&] ^
)&&^ _
;&&_ `
}'' 
}(( ≠>
à/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/DeterminismValidatingMatchManager.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class -
!DeterminismValidatingMatchManager .
<. /

TGameState/ 9
>9 :
where; @

TGameStateA K
:L M
NetworkGameStateN ^
{ 
private		 
readonly		 
MatchManager		 !
<		! "

TGameState		" ,
>		, -
_innerManager		. ;
;		; <
private

 
readonly

 
IGameStateFactory

 &
<

& '

TGameState

' 1
>

1 2
_gameStateFactory

3 D
;

D E
private 
readonly 

Dictionary 
<  
Guid  $
,$ % 
DeterminismValidator& :
<: ;

TGameState; E
>E F
>F G
_validatorsH S
=T U
newV Y
(Y Z
)Z [
;[ \
private 
readonly 
object 
_lock !
=" #
new$ '
(' (
)( )
;) *
public 
 
DeterminismValidator 
<  

TGameState  *
>* +
?+ ,
GetValidator- 9
(9 :
Guid: >
matchId? F
)F G
{ 
lock 
( 
_lock 
) 
{ 	
return 
_validators 
. 
GetValueOrDefault 0
(0 1
matchId1 8
)8 9
;9 :
} 	
} 
public 
-
!DeterminismValidatingMatchManager ,
(, -
MatchManager 
< 

TGameState 
>  
innerManager! -
,- .
IGameStateFactory 
< 

TGameState $
>$ %
gameStateFactory& 6
)6 7
{ 
_innerManager 
= 
innerManager $
;$ %
_gameStateFactory 
= 
gameStateFactory ,
;, -
_innerManager!! 
.!! 
OnMatchCreated!! $
+=!!% '
OnMatchCreated!!( 6
;!!6 7
}"" 
private$$ 
void$$ 
OnMatchCreated$$ 
($$  
Guid$$  $
matchId$$% ,
,$$, -

TGameState$$. 8
primary$$9 @
)$$@ A
{%% 
if&& 

(&& 
!&&  
DeterminismValidator&& !
<&&! "

TGameState&&" ,
>&&, -
.&&- .
	IsEnabled&&. 7
)&&7 8
{'' 	
return(( 
;(( 
})) 	
lock++ 
(++ 
_lock++ 
)++ 
{,, 	
var// 
shadow// 
=// 
_gameStateFactory// *
.//* +
CreateGameState//+ :
(//: ;
matchId//; B
)//B C
;//C D
Console33 
.33 
	WriteLine33 
(33 
$"33  
$str33  [
{33[ \
primary33\ c
.33c d
History33d k
.33k l
Count33l q
}33q r
"33r s
)33s t
;33t u
if55 
(55 
primary55 
.55 
History55 
.55  
Count55  %
>55& '
$num55( )
)55) *
{66 
Console77 
.77 
	WriteLine77 !
(77! "
$"77" $
$str77$ u
"77u v
)77v w
;77w x
Console88 
.88 
	WriteLine88 !
(88! "
$"88" $
$str88$ l
"88l m
)88m n
;88n o
Console99 
.99 
	WriteLine99 !
(99! "
$"99" $
$str99$ f
"99f g
)99g h
;99h i
}:: 
var<< 
	validator<< 
=<< 
new<<  
DeterminismValidator<<  4
<<<4 5

TGameState<<5 ?
><<? @
(<<@ A
primary<<A H
,<<H I
shadow<<J P
,<<P Q
matchId<<R Y
)<<Y Z
;<<Z [
_validators== 
[== 
matchId== 
]==  
===! "
	validator==# ,
;==, -
Console?? 
.?? 
	WriteLine?? 
(?? 
$"??  
$str??  ]
{??] ^
matchId??^ e
}??e f
"??f g
)??g h
;??h i
}@@ 	
}AA 
publicFF 

voidFF 
OnMatchRemovedFF 
(FF 
GuidFF #
matchIdFF$ +
)FF+ ,
{GG 
lockHH 
(HH 
_lockHH 
)HH 
{II 	
ifJJ 
(JJ 
_validatorsJJ 
.JJ 
RemoveJJ "
(JJ" #
matchIdJJ# *
,JJ* +
outJJ, /
varJJ0 3
	validatorJJ4 =
)JJ= >
)JJ> ?
{KK 
	validatorLL 
.LL 
ShadowLL  
.LL  !
DisposeLL! (
(LL( )
)LL) *
;LL* +
ConsoleMM 
.MM 
	WriteLineMM !
(MM! "
$"MM" $
$strMM$ d
{MMd e
matchIdMMe l
}MMl m
"MMm n
)MMn o
;MMo p
}NN 
}OO 	
}PP 
publicUU 

IEnumerableUU 
<UU 
(UU 
GuidUU 
MatchIdUU $
,UU$ %
intUU& )
ActionCountUU* 5
,UU5 6
boolUU7 ;
	HasFailedUU< E
)UUE F
>UUF G!
GetValidatorSummariesUUH ]
(UU] ^
)UU^ _
{VV 
lockWW 
(WW 
_lockWW 
)WW 
{XX 	
returnYY 
_validatorsYY 
.YY 
SelectYY %
(YY% &
kvYY& (
=>YY) +
(YY, -
kvYY- /
.YY/ 0
KeyYY0 3
,YY3 4
kvYY5 7
.YY7 8
ValueYY8 =
.YY= >
ActionCountYY> I
,YYI J
kvYYK M
.YYM N
ValueYYN S
.YYS T
	HasFailedYYT ]
)YY] ^
)YY^ _
.YY_ `
ToListYY` f
(YYf g
)YYg h
;YYh i
}ZZ 	
}[[ 
publicaa 

voidaa !
InstallValidationHookaa %
(aa% &
Guidaa& *
matchIdaa+ 2
,aa2 3!
NetworkActionExecutoraa4 I
executoraaJ R
)aaR S
{bb 
varcc 
	validatorcc 
=cc 
GetValidatorcc $
(cc$ %
matchIdcc% ,
)cc, -
;cc- .
ifdd 

(dd 
	validatordd 
==dd 
nulldd 
)dd 
returndd %
;dd% &
executorff 
.ff 
ValidationHookff 
=ff  !
(ff" #
actionff# )
,ff) *
executePrimaryff+ 9
)ff9 :
=>ff; =
{gg 	
ifii 
(ii 
actionii 
.ii 
GetTypeii 
(ii 
)ii  
.ii  !
Nameii! %
==ii& (
$strii) >
)ii> ?
{jj 
executePrimarykk 
(kk 
)kk  
;kk  !
returnll 
truell 
;ll 
}mm 
returnoo 
	validatoroo 
.oo 
ValidateActionoo +
(oo+ ,
actionoo, 2
,oo2 3
executePrimaryoo4 B
)ooB C
;ooC D
}pp 	
;pp	 

}qq 
}rr Ô

~/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/DefaultGameStateFactory.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class #
DefaultGameStateFactory $
<$ %

TGameState% /
>/ 0
:1 2
IGameStateFactory3 D
<D E

TGameStateE O
>O P
where 	

TGameState
 
: 
NetworkGameState '
{		 
private

 
readonly

 
Func

 
<

 
Guid

 
,

 

TGameState

  *
>

* +
_factory

, 4
;

4 5
public 
#
DefaultGameStateFactory "
(" #
Func# '
<' (
Guid( ,
,, -

TGameState. 8
>8 9
factory: A
)A B
{ 
_factory 
= 
factory 
; 
} 
public 


TGameState 
CreateGameState %
(% &
Guid& *
matchId+ 2
)2 3
{ 
return 
_factory 
( 
matchId 
)  
;  !
} 
} ¿
u/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/DefaultGameHub.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public 
class 
DefaultGameHub 
< 

TGameState &
>& '
:( )
GameHub* 1
<1 2
MatchManager2 >
<> ?

TGameState? I
>I J
,J K

TGameStateL V
>V W
where 	

TGameState
 
: 
NetworkGameState '
{		 
public

 

DefaultGameHub

 
(

 
ServerDomain

 &
serverDomain

' 3
,

3 4
MatchManager

5 A
<

A B

TGameState

B L
>

L M
matchManager

N Z
)

Z [
: 	
base
 
( 
serverDomain 
, 
matchManager )
)) *
{ 
} 
} ÿ#
Ü/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/CollectiveActions/VoteForAction.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
., -
CollectiveActions- >
;> ?
public 
abstract 
class 
VoteForAction #
<# $
TDomain$ +
,+ ,
TAction- 4
>4 5
:6 7
NetworkAction8 E
<E F
TDomainF M
,M N
TActionO V
>V W
,W X
IVoteForActionY g
,g h,
IRequireCollectiveActionManager	i à
where 	
TDomain
 
: 
class 
, 
IDomain "
where		 	
TAction		
 
:		 
class		 
,		 
INetworkAction		 )
{

 
public 

abstract 
string 
CollectiveKey (
{) *
get+ .
;. /
}0 1
public 

abstract 
string 

VoteOption %
{& '
get( +
;+ ,
}- .
[ 

JsonIgnore 
] 
public #
CollectiveActionManager /
?/ 0#
CollectiveActionManager1 H
{I J
getK N
;N O
setP S
;S T
}U V
[ 

JsonIgnore 
] 
public 
bool 
IsWinningExecution /
{0 1
get2 5
;5 6
set7 :
;: ;
}< =
[ 

JsonIgnore 
] 
public 

Dictionary "
<" #
string# )
,) *
int+ .
>. /
?/ 0
FinalVoteCounts1 @
{A B
getC F
;F G
setH K
;K L
}M N
[ 

JsonIgnore 
] 
private 
TDomain  
?  !
_executionDomain" 2
;2 3
	protected 
sealed 
override 
void "
ExecuteProcess# 1
(1 2
TDomain2 9
domain: @
)@ A
{ 
if 

( #
CollectiveActionManager #
==$ &
null' +
)+ ,
{ 	
Console 
. 
	WriteLine 
( 
$"  
$str  X
{X Y
GetTypeY `
(` a
)a b
.b c
Namec g
}g h
"h i
)i j
;j k
return 
; 
} 	
_executionDomain 
= 
domain !
;! "
OnVoteSubmitted 
( 
domain 
) 
;  #
CollectiveActionManager 
.  

SubmitVote  *
(* +
this+ /
)/ 0
;0 1
} 
	protected   
abstract   
void   
ExecuteWhenWon   *
(  * +
TDomain  + 2
domain  3 9
)  9 :
;  : ;
	protected"" 
virtual"" 
void"" 
OnVoteSubmitted"" *
(""* +
TDomain""+ 2
domain""3 9
)""9 :
{## 
Console$$ 
.$$ 
	WriteLine$$ 
($$ 
$"$$ 
$str$$ 3
{$$3 4

ExecutorId$$4 >
}$$> ?
$str$$? J
{$$J K

VoteOption$$K U
}$$U V
$str$$V Z
{$$Z [
CollectiveKey$$[ h
}$$h i
"$$i j
)$$j k
;$$k l
}%% 
public'' 

void'' #
TriggerWinningExecution'' '
(''' (

Dictionary''( 2
<''2 3
string''3 9
,''9 :
int''; >
>''> ?

voteCounts''@ J
)''J K
{(( 
IsWinningExecution)) 
=)) 
true)) !
;))! "
FinalVoteCounts** 
=** 

voteCounts** $
;**$ %
if++ 

(++ 
_executionDomain++ 
!=++ 
null++  $
)++$ %
ExecuteWhenWon,, 
(,, 
_executionDomain,, +
),,+ ,
;,,, -
}-- 
}.. £
ò/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/CollectiveActions/IRequireCollectiveActionManager.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
., -
CollectiveActions- >
;> ?
public 
	interface +
IRequireCollectiveActionManager 0
:1 2

IDARAction3 =
{ #
CollectiveActionManager 
? #
CollectiveActionManager 4
{5 6
get7 :
;: ;
set< ?
;? @
}A B
} Æ	
ä/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/CollectiveActions/ICollectiveAction.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
., -
CollectiveActions- >
;> ?
public 
	interface 
ICollectiveAction "
:# $
INetworkAction% 3
{ 
string 

CollectiveKey 
{ 
get 
; 
}  !
} 
public

 
	interface

 
IWaitForAllAction

 "
:

# $
ICollectiveAction

% 6
{ 
} 
public 
	interface 
IVoteForAction 
:  !
ICollectiveAction" 3
{ 
string 


VoteOption 
{ 
get 
; 
} 
void #
TriggerWinningExecution	  
(  !

Dictionary! +
<+ ,
string, 2
,2 3
int4 7
>7 8

voteCounts9 C
)C D
;D E
} ∑ó
ê/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/CollectiveActions/CollectiveActionManager.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
., -
CollectiveActions- >
;> ?
public 
class #
CollectiveActionManager $
:% &

LeafDomain' 1
{		 
private

 
readonly

 

Dictionary

 
<

  
string

  &
,

& '
HashSet

( /
<

/ 0
Guid

0 4
>

4 5
>

5 6"
_waitForAllSubmissions

7 M
=

N O
new

P S
(

S T
)

T U
;

U V
private 
readonly 

Dictionary 
<  
string  &
,& '
IWaitForAllAction( 9
>9 :
_waitForAllActions; M
=N O
newP S
(S T
)T U
;U V
private 
readonly 

Dictionary 
<  
string  &
,& '
HashSet( /
</ 0
Guid0 4
>4 5
>5 6&
_waitForAllRequiredPlayers7 Q
=R S
newT W
(W X
)X Y
;Y Z
private 
readonly 

Dictionary 
<  
string  &
,& '

Dictionary( 2
<2 3
string3 9
,9 :
HashSet; B
<B C
GuidC G
>G H
>H I
>I J
_voteSubmissionsK [
=\ ]
new^ a
(a b
)b c
;c d
private 
readonly 

Dictionary 
<  
string  &
,& '

Dictionary( 2
<2 3
string3 9
,9 :
IVoteForAction; I
>I J
>J K
_voteActionsL X
=Y Z
new[ ^
(^ _
)_ `
;` a
private 
readonly 

Dictionary 
<  
string  &
,& '
HashSet( /
</ 0
Guid0 4
>4 5
>5 6 
_voteRequiredPlayers7 K
=L M
newN Q
(Q R
)R S
;S T
private 
readonly 
Func 
< 
IEnumerable %
<% &
Guid& *
>* +
>+ ,
_getPlayerIds- :
;: ;
public 

event 
Action 
< 
IWaitForAllAction )
>) *
?* +
OnWaitForAllReady, =
;= >
public 

event 
Action 
< 
IVoteForAction &
,& '

Dictionary( 2
<2 3
string3 9
,9 :
int; >
>> ?
>? @
?@ A
OnVoteResolvedB P
;P Q
public 
#
CollectiveActionManager "
(" #
BranchDomain# /
parent0 6
,6 7
Func8 <
<< =
IEnumerable= H
<H I
GuidI M
>M N
>N O
getPlayerIdsP \
)\ ]
:^ _
base` d
(d e
parente k
)k l
{ 
_getPlayerIds 
= 
getPlayerIds $
;$ %
new 
Reaction 
< 

LeafDomain 
,  +
IRequireCollectiveActionManager! @
>@ A
(A B
parentB H
)H I
. 
Prepare 
( 
( 
_ 
, 
action 
)  
=>! #
action$ *
.* +#
CollectiveActionManager+ B
=C D
thisE I
)I J
. 
AddTo 
( 
Disposables 
) 
;  
} 
public   

bool   
SubmitWaitForAll    
(    !
IWaitForAllAction  ! 2
action  3 9
)  9 :
{!! 
var"" 
playerId"" 
="" 
action"" 
."" 

ExecutorId"" (
;""( )
if## 

(## 
playerId## 
==## 
null## 
)## 
return## $
false##% *
;##* +
var%% 
key%% 
=%% 
action%% 
.%% 
CollectiveKey%% &
;%%& '
if'' 

('' 
!'' "
_waitForAllSubmissions'' #
.''# $
ContainsKey''$ /
(''/ 0
key''0 3
)''3 4
)''4 5
{(( 	"
_waitForAllSubmissions)) "
[))" #
key))# &
]))& '
=))( )
new))* -
HashSet)). 5
<))5 6
Guid))6 :
>)): ;
()); <
)))< =
;))= >
_waitForAllActions** 
[** 
key** "
]**" #
=**$ %
action**& ,
;**, -&
_waitForAllRequiredPlayers++ &
[++& '
key++' *
]++* +
=++, -
_getPlayerIds++. ;
(++; <
)++< =
.++= >
	ToHashSet++> G
(++G H
)++H I
;++I J
},, 	
var.. 
requiredPlayers.. 
=.. &
_waitForAllRequiredPlayers.. 8
[..8 9
key..9 <
]..< =
;..= >
if// 

(// 
!// 
requiredPlayers// 
.// 
Contains// %
(//% &
playerId//& .
.//. /
Value/// 4
)//4 5
)//5 6
{00 	
Console11 
.11 
	WriteLine11 
(11 
$"11  
$str11  A
{11A B
playerId11B J
}11J K
$str11K h
{11h i
key11i l
}11l m
"11m n
)11n o
;11o p
return22 
false22 
;22 
}33 	"
_waitForAllSubmissions55 
[55 
key55 "
]55" #
.55# $
Add55$ '
(55' (
playerId55( 0
.550 1
Value551 6
)556 7
;557 8
var77 
submittedIds77 
=77 "
_waitForAllSubmissions77 1
[771 2
key772 5
]775 6
;776 7
if88 

(88 
requiredPlayers88 
.88 
All88 
(88  
id88  "
=>88# %
submittedIds88& 2
.882 3
Contains883 ;
(88; <
id88< >
)88> ?
)88? @
)88@ A
{99 	
var:: 
actionToExecute:: 
=::  !
_waitForAllActions::" 4
[::4 5
key::5 8
]::8 9
;::9 :"
_waitForAllSubmissions;; "
.;;" #
Remove;;# )
(;;) *
key;;* -
);;- .
;;;. /
_waitForAllActions<< 
.<< 
Remove<< %
(<<% &
key<<& )
)<<) *
;<<* +&
_waitForAllRequiredPlayers== &
.==& '
Remove==' -
(==- .
key==. 1
)==1 2
;==2 3
OnWaitForAllReady?? 
??? 
.?? 
Invoke?? %
(??% &
actionToExecute??& 5
)??5 6
;??6 7
return@@ 
true@@ 
;@@ 
}AA 	
returnCC 
falseCC 
;CC 
}DD 
publicFF 

boolFF 

SubmitVoteFF 
(FF 
IVoteForActionFF )
actionFF* 0
)FF0 1
{GG 
varHH 
playerIdHH 
=HH 
actionHH 
.HH 

ExecutorIdHH (
;HH( )
ifII 

(II 
playerIdII 
==II 
nullII 
)II 
returnII $
falseII% *
;II* +
varKK 
keyKK 
=KK 
actionKK 
.KK 
CollectiveKeyKK &
;KK& '
varLL 
optionLL 
=LL 
actionLL 
.LL 

VoteOptionLL &
;LL& '
ifNN 

(NN 
!NN 
_voteSubmissionsNN 
.NN 
ContainsKeyNN )
(NN) *
keyNN* -
)NN- .
)NN. /
{OO 	
_voteSubmissionsPP 
[PP 
keyPP  
]PP  !
=PP" #
newPP$ '

DictionaryPP( 2
<PP2 3
stringPP3 9
,PP9 :
HashSetPP; B
<PPB C
GuidPPC G
>PPG H
>PPH I
(PPI J
)PPJ K
;PPK L
_voteActionsQQ 
[QQ 
keyQQ 
]QQ 
=QQ 
newQQ  #

DictionaryQQ$ .
<QQ. /
stringQQ/ 5
,QQ5 6
IVoteForActionQQ7 E
>QQE F
(QQF G
)QQG H
;QQH I 
_voteRequiredPlayersRR  
[RR  !
keyRR! $
]RR$ %
=RR& '
_getPlayerIdsRR( 5
(RR5 6
)RR6 7
.RR7 8
	ToHashSetRR8 A
(RRA B
)RRB C
;RRC D
}SS 	
varUU 
requiredPlayersUU 
=UU  
_voteRequiredPlayersUU 2
[UU2 3
keyUU3 6
]UU6 7
;UU7 8
ifVV 

(VV 
!VV 
requiredPlayersVV 
.VV 
ContainsVV %
(VV% &
playerIdVV& .
.VV. /
ValueVV/ 4
)VV4 5
)VV5 6
{WW 	
ConsoleXX 
.XX 
	WriteLineXX 
(XX 
$"XX  
$strXX  A
{XXA B
playerIdXXB J
}XXJ K
$strXXK m
{XXm n
keyXXn q
}XXq r
"XXr s
)XXs t
;XXt u
returnYY 
falseYY 
;YY 
}ZZ 	
foreach\\ 
(\\ 
var\\ 
optionVotes\\  
in\\! #
_voteSubmissions\\$ 4
[\\4 5
key\\5 8
]\\8 9
.\\9 :
Values\\: @
)\\@ A
{]] 	
optionVotes^^ 
.^^ 
Remove^^ 
(^^ 
playerId^^ '
.^^' (
Value^^( -
)^^- .
;^^. /
}__ 	
ifaa 

(aa 
!aa 
_voteSubmissionsaa 
[aa 
keyaa !
]aa! "
.aa" #
ContainsKeyaa# .
(aa. /
optionaa/ 5
)aa5 6
)aa6 7
{bb 	
_voteSubmissionscc 
[cc 
keycc  
]cc  !
[cc! "
optioncc" (
]cc( )
=cc* +
newcc, /
HashSetcc0 7
<cc7 8
Guidcc8 <
>cc< =
(cc= >
)cc> ?
;cc? @
_voteActionsdd 
[dd 
keydd 
]dd 
[dd 
optiondd $
]dd$ %
=dd& '
actiondd( .
;dd. /
}ee 	
_voteSubmissionsff 
[ff 
keyff 
]ff 
[ff 
optionff $
]ff$ %
.ff% &
Addff& )
(ff) *
playerIdff* 2
.ff2 3
Valueff3 8
)ff8 9
;ff9 :
varhh 
	allVotershh 
=hh 
_voteSubmissionshh (
[hh( )
keyhh) ,
]hh, -
.hh- .
Valueshh. 4
.hh4 5

SelectManyhh5 ?
(hh? @
vhh@ A
=>hhB D
vhhE F
)hhF G
.hhG H
	ToHashSethhH Q
(hhQ R
)hhR S
;hhS T
ifii 

(ii 
requiredPlayersii 
.ii 
Allii 
(ii  
idii  "
=>ii# %
	allVotersii& /
.ii/ 0
Containsii0 8
(ii8 9
idii9 ;
)ii; <
)ii< =
)ii= >
{jj 	
varkk 

voteCountskk 
=kk 
_voteSubmissionskk -
[kk- .
keykk. 1
]kk1 2
.ll 
ToDictionaryll 
(ll 
kvll  
=>ll! #
kvll$ &
.ll& '
Keyll' *
,ll* +
kvll, .
=>ll/ 1
kvll2 4
.ll4 5
Valuell5 :
.ll: ;
Countll; @
)ll@ A
;llA B
varnn 
winningOptionnn 
=nn 

voteCountsnn  *
.oo 
OrderByDescendingoo "
(oo" #
kvoo# %
=>oo& (
kvoo) +
.oo+ ,
Valueoo, 1
)oo1 2
.pp 
Firstpp 
(pp 
)pp 
.qq 
Keyqq 
;qq 
varss 
winningActionss 
=ss 
_voteActionsss  ,
[ss, -
keyss- 0
]ss0 1
[ss1 2
winningOptionss2 ?
]ss? @
;ss@ A
_voteSubmissionsuu 
.uu 
Removeuu #
(uu# $
keyuu$ '
)uu' (
;uu( )
_voteActionsvv 
.vv 
Removevv 
(vv  
keyvv  #
)vv# $
;vv$ % 
_voteRequiredPlayersww  
.ww  !
Removeww! '
(ww' (
keyww( +
)ww+ ,
;ww, -
winningActionyy 
.yy #
TriggerWinningExecutionyy 1
(yy1 2

voteCountsyy2 <
)yy< =
;yy= >
OnVoteResolvedzz 
?zz 
.zz 
Invokezz "
(zz" #
winningActionzz# 0
,zz0 1

voteCountszz2 <
)zz< =
;zz= >
return{{ 
true{{ 
;{{ 
}|| 	
return~~ 
false~~ 
;~~ 
} 
public
ÅÅ 

(
ÅÅ 
int
ÅÅ 
	submitted
ÅÅ 
,
ÅÅ 
int
ÅÅ 
total
ÅÅ $
)
ÅÅ$ %!
GetWaitForAllStatus
ÅÅ& 9
(
ÅÅ9 :
string
ÅÅ: @
collectiveKey
ÅÅA N
)
ÅÅN O
{
ÇÇ 
var
ÉÉ 
total
ÉÉ 
=
ÉÉ (
_waitForAllRequiredPlayers
ÉÉ .
.
ÉÉ. /
TryGetValue
ÉÉ/ :
(
ÉÉ: ;
collectiveKey
ÉÉ; H
,
ÉÉH I
out
ÉÉJ M
var
ÉÉN Q
required
ÉÉR Z
)
ÉÉZ [
?
ÑÑ 
required
ÑÑ 
.
ÑÑ 
Count
ÑÑ 
:
ÖÖ 
_getPlayerIds
ÖÖ 
(
ÖÖ 
)
ÖÖ 
.
ÖÖ 
Count
ÖÖ #
(
ÖÖ# $
)
ÖÖ$ %
;
ÖÖ% &
var
ÜÜ 
	submitted
ÜÜ 
=
ÜÜ $
_waitForAllSubmissions
ÜÜ .
.
ÜÜ. /
TryGetValue
ÜÜ/ :
(
ÜÜ: ;
collectiveKey
ÜÜ; H
,
ÜÜH I
out
ÜÜJ M
var
ÜÜN Q
set
ÜÜR U
)
ÜÜU V
?
ÜÜW X
set
ÜÜY \
.
ÜÜ\ ]
Count
ÜÜ] b
:
ÜÜc d
$num
ÜÜe f
;
ÜÜf g
return
áá 
(
áá 
	submitted
áá 
,
áá 
total
áá  
)
áá  !
;
áá! "
}
àà 
public
ää 


Dictionary
ää 
<
ää 
string
ää 
,
ää 
int
ää !
>
ää! "
GetVoteCounts
ää# 0
(
ää0 1
string
ää1 7
collectiveKey
ää8 E
)
ääE F
{
ãã 
if
åå 

(
åå 
!
åå 
_voteSubmissions
åå 
.
åå 
TryGetValue
åå )
(
åå) *
collectiveKey
åå* 7
,
åå7 8
out
åå9 <
var
åå= @
submissions
ååA L
)
ååL M
)
ååM N
return
çç 
new
çç 

Dictionary
çç !
<
çç! "
string
çç" (
,
çç( )
int
çç* -
>
çç- .
(
çç. /
)
çç/ 0
;
çç0 1
return
èè 
submissions
èè 
.
èè 
ToDictionary
èè '
(
èè' (
kv
èè( *
=>
èè+ -
kv
èè. 0
.
èè0 1
Key
èè1 4
,
èè4 5
kv
èè6 8
=>
èè9 ;
kv
èè< >
.
èè> ?
Value
èè? D
.
èèD E
Count
èèE J
)
èèJ K
;
èèK L
}
êê 
public
íí 

bool
íí 
HasSubmitted
íí 
(
íí 
string
íí #
collectiveKey
íí$ 1
,
íí1 2
Guid
íí3 7
playerId
íí8 @
)
íí@ A
{
ìì 
return
îî $
_waitForAllSubmissions
îî %
.
îî% &
TryGetValue
îî& 1
(
îî1 2
collectiveKey
îî2 ?
,
îî? @
out
îîA D
var
îîE H
set
îîI L
)
îîL M
&&
îîN P
set
îîQ T
.
îîT U
Contains
îîU ]
(
îî] ^
playerId
îî^ f
)
îîf g
;
îîg h
}
ïï 
public
óó 

bool
óó 
HasVoted
óó 
(
óó 
string
óó 
collectiveKey
óó  -
,
óó- .
Guid
óó/ 3
playerId
óó4 <
)
óó< =
{
òò 
if
ôô 

(
ôô 
!
ôô 
_voteSubmissions
ôô 
.
ôô 
TryGetValue
ôô )
(
ôô) *
collectiveKey
ôô* 7
,
ôô7 8
out
ôô9 <
var
ôô= @
submissions
ôôA L
)
ôôL M
)
ôôM N
return
öö 
false
öö 
;
öö 
return
úú 
submissions
úú 
.
úú 
Values
úú !
.
úú! "
Any
úú" %
(
úú% &
voters
úú& ,
=>
úú- /
voters
úú0 6
.
úú6 7
Contains
úú7 ?
(
úú? @
playerId
úú@ H
)
úúH I
)
úúI J
;
úúJ K
}
ùù 
public
üü 

void
üü $
CancelCollectiveAction
üü &
(
üü& '
string
üü' -
collectiveKey
üü. ;
)
üü; <
{
†† $
_waitForAllSubmissions
°° 
.
°° 
Remove
°° %
(
°°% &
collectiveKey
°°& 3
)
°°3 4
;
°°4 5 
_waitForAllActions
¢¢ 
.
¢¢ 
Remove
¢¢ !
(
¢¢! "
collectiveKey
¢¢" /
)
¢¢/ 0
;
¢¢0 1(
_waitForAllRequiredPlayers
££ "
.
££" #
Remove
££# )
(
££) *
collectiveKey
££* 7
)
££7 8
;
££8 9
_voteSubmissions
§§ 
.
§§ 
Remove
§§ 
(
§§  
collectiveKey
§§  -
)
§§- .
;
§§. /
_voteActions
•• 
.
•• 
Remove
•• 
(
•• 
collectiveKey
•• )
)
••) *
;
••* +"
_voteRequiredPlayers
¶¶ 
.
¶¶ 
Remove
¶¶ #
(
¶¶# $
collectiveKey
¶¶$ 1
)
¶¶1 2
;
¶¶2 3
}
ßß 
}®® „
}/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/ClientDomainExtensions.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public		 
static		 
class		 "
ClientDomainExtensions		 *
{

 
public 

static 
async 
Task 
< 
HubConnection *
>* +
ConnectToServer, ;
<; <

TGameState< F
>F G
(G H
this 
ClientDomain 
< 

TGameState $
>$ %
client& ,
,, -
string 
	serverUrl 
) 
where 

TGameState 
: 
NetworkGameState +
{ 
var 
url 
= 
$" 
{ 
	serverUrl 
} 
$str '
{' (
client( .
.. /
UserId/ 5
}5 6
$str6 ?
{? @
client@ F
.F G
MatchIdG N
}N O
"O P
;P Q
var 

connection 
= 
new  
HubConnectionBuilder 1
(1 2
)2 3
. 
WithUrl 
( 
url 
) 
. 
Build 
( 
) 
; 
client 
. 
NetworkSyncManager !
.! "
OnSync" (
+=) +
async, 1
(2 3
matchId3 :
,: ;
actions< C
)C D
=>E G
{ 	
var 
json 
= 
JsonSerializer %
.% &
ToJson& ,
(, -
actions- 4
)4 5
;5 6
await 

connection 
. 
InvokeAsync (
(( )
$str) 6
,6 7
json8 <
)< =
;= >
}   	
;  	 


connection## 
.## 
On## 
<## 
string## 
>## 
(## 
$str## +
,##+ ,
actionsJson##- 8
=>##9 ;
{$$ 	
var%% 
executor%% 
=%% 
new%% !
NetworkActionExecutor%% 4
(%%4 5
client%%5 ;
.%%; <
	GameState%%< E
.%%E F
Registry%%F N
)%%N O
;%%O P
executor&& 
.&& 
ExecuteBatch&& !
(&&! "
actionsJson&&" -
)&&- .
;&&. /
}'' 	
)''	 

;''
 
await** 

connection** 
.** 

StartAsync** #
(**# $
)**$ %
;**% &
return,, 

connection,, 
;,, 
}-- 
}.. „
s/Users/a1/Documents/GitHub/turn-based-prototype/Server/Framework/Deterministic.GameFramework.Server/ClientDomain.cs
	namespace 	
Deterministic
 
. 
GameFramework %
.% &
Server& ,
;, -
public

 
class

 
ClientDomain

 
<

 

TGameState

 $
>

$ %
:

& '

RootDomain

( 2
where

3 8

TGameState

9 C
:

D E
NetworkGameState

F V
{ 
public 

static 
ClientDomain 
< 

TGameState )
>) *
?* +
Instance, 4
{5 6
get7 :
;: ;
	protected< E
setF I
;I J
}K L
public 

Guid 
UserId 
{ 
get 
; 
} 
public 

Guid 
MatchId 
{ 
get 
; 
}  
public 

GameLoop 
GameLoop 
{ 
get "
;" #
}$ %
public 

NetworkSyncManager 
NetworkSyncManager 0
{1 2
get3 6
;6 7
}8 9
public 


TGameState 
	GameState 
{  !
get" %
;% &
}' (
public 

ClientDomain 
( 
Guid 
userId #
,# $
Guid% )
matchId* 1
,1 2

TGameState3 =
	gameState> G
)G H
{ 
Instance 
= 
this 
; 
UserId 
= 
userId 
; 
MatchId 
= 
matchId 
; 
GameLoop 
= 
new 
GameLoop 
(  
this  $
)$ %
;% &
GameLoop 
. 
SetTargetFps 
( 
$num  
)  !
;! "
_ 	
=
 
GameLoop 
. 
Start 
( 
) 
; 
NetworkSyncManager   
=   
new    
NetworkSyncManager  ! 3
(  3 4
this  4 8
)  8 9
;  9 :
	GameState## 
=## 
	gameState## 
;## 

Subdomains$$ 
.$$ 
Add$$ 
($$ 
	GameState$$  
)$$  !
;$$! "
}%% 
public++ 

void++ 
Send++ 
(++ 
INetworkAction++ #
action++$ *
)++* +
{,, 
action-- 
.-- 

ExecutorId-- 
=-- 
UserId-- "
;--" #
new.. 

SendAction.. 
(.. 
action.. 
,.. 
	GameState.. (
)..( )
...) *
Execute..* 1
(..1 2
this..2 6
)..6 7
;..7 8
}// 
}00 