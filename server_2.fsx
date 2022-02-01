// ActorSayHello.fsx
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote"
#r "nuget: Suave"

#r "nuget: FSharp.Json" 
open FSharp.Json
open System
open System.Net
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open System.Collections.Generic
open System.Collections.Concurrent
open System.Text.RegularExpressions
open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket


//*********************************______________________________________________________*************************************************_______________________________________________________________________________________________

//Global variables
let TotalUsers = 1000
let LevelOfConcurrency = 16
let ServerInstances = 32

//Type declaration 
type Test = int
type RegisterUser = string * string     // username passowrd
type Follow = int* string * string   // int for identification fo match no use ,UserID of user to be followed , self userid 
type Tweet = int * int * string * string * string * string
type Retweet = int * int * int * int * int * string *  string * string
type HashTagQ = int * int * int * string
type MentionQ = int * int * int * int * string
type GetUserTweets = int * int * int * int * int * string  
type Login = int * int * int * string * string 
type Logout = int * int * int * int * string * string  // int for identification fo match no use 
let mutable WebsocketGlobal = 1

//Concurrent dictionary for thread operation 
let mutable userTable: ConcurrentDictionary<string,string> = new ConcurrentDictionary<string,string>(LevelOfConcurrency, TotalUsers)
let mutable tweetTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(LevelOfConcurrency, TotalUsers)
let mutable feedTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(LevelOfConcurrency, TotalUsers)
let mutable followersTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(LevelOfConcurrency, TotalUsers)
let mutable subscriptionTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(LevelOfConcurrency, TotalUsers)
let mutable hashtagTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(LevelOfConcurrency, TotalUsers)
let mutable mentionsTable: ConcurrentDictionary<string,ConcurrentBag<string>> = new ConcurrentDictionary<string,ConcurrentBag<string>>(LevelOfConcurrency, TotalUsers)
let mutable UpUsers : List<string> = new List<string>()
let mutable WebSocketTable: ConcurrentDictionary<string,WebSocket> = new ConcurrentDictionary<string,WebSocket>(LevelOfConcurrency, TotalUsers)

//_______________________________*********************************_________________________________________*******************************___________________________________________________________________________________________

let mutable ShutDownExecution = false
let mutable count = 0

type RequestJsonObj= {
  RequestType : string;
  UserId : string;
  Password : string;
  TweetNumber : int
  Tweet : string;
  FollowUserId : string;
  SubscriptionUserName : string;
  Mention : string;
  HashTag : string
}

type ResponseJsonObj= {
  RequestType : string;
  RequestStatus : string;
  ResponseString1 : string;
  ResponseString2 : string
  
}

let mutable responseJsonDummy : ResponseJsonObj = {RequestType = "Register" ;RequestStatus = "100" ; ResponseString1 = "Good Morning" ; ResponseString2 = "Heyy" }

// Actor system 
let system = ActorSystem.Create("ActorSystemSupervisor")

// check if the hashtags and the mentions are matching
let CompareHashtagsAndMentions (inputString : string) =
    let mutable regexPattern1 = "(?:^|\s+)(?:(?<mention>@)|(?<hash>#))(?<item>\w+)(?=\s+)"
    let mutable regexPattern2 =  "\B#\w\w+"
    let regexPatt1 = new Regex(regexPattern1)
    let regexPatt2 = new Regex(regexPattern2)
    let MatchedMentions = regexPatt1.Matches inputString
    let MatchedHashtags = regexPatt2.Matches inputString
    let mutable ListOfMentions : List<string> = new List<string>()
    let mutable ListOfHashtags : List<string> = new List<string>()

    // Add mentions and hashtags to the list 
    for i in 0..MatchedMentions.Count - 1 do
        let mutable valss : string = (MatchedMentions.Item(i)).ToString().Trim()
        if(valss.[0] = '@') then
            ListOfMentions.Add(valss)         
    for i in 0..MatchedHashtags.Count - 1 do
        let mutable valss : string = (MatchedHashtags.Item(i)).ToString().Trim()
        if (valss.[0] = '#') then
                ListOfHashtags.Add(valss)
    (ListOfMentions,ListOfHashtags)

let mutable ServerIdCount : int = 0
let getServerID() : int =
    if (ServerIdCount >= ServerInstances) then
        ServerIdCount <- 0
    ServerIdCount <- ServerIdCount + 1
    ServerIdCount


// server response
let sendResponse (response : string ,webSocketObj : WebSocket ) = 
    socket {  
           let byteResponse =
               response
               |> System.Text.Encoding.ASCII.GetBytes
               |> ByteSegment
           do! webSocketObj.send Text byteResponse true 
    }


//________________________________________________________**********************************************________________________________________________________*****************************

// Actor 
type ServerActor() =
  inherit Actor()
  override x.OnReceive message =
     match message with
     | :? Test as msg -> let mutable response : string = "Hey there, actor is working"
                         let mutable webSocketObj : WebSocket = WebSocketTable.Item("tweetUser-1")
                         while (true) do
                                sendResponse(response , webSocketObj)
                              

     | :? RegisterUser as msg -> let (username , password) = unbox<RegisterUser> msg
                                 let mutable DummyVariable = 0
                                 let mutable SuccessfulRegistration : bool = false
                                 let mutable UsernameAlreadyTaken: bool = userTable.ContainsKey(username)
                                 if(UsernameAlreadyTaken) then

                                    let mutable dataString : string = "User name - " + username + " is not available, try using different user name."
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "RegisterUser" ; RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson
                                 else
                                    while (not SuccessfulRegistration) do
                                        let mutable TweetBag : ConcurrentBag<string> = new ConcurrentBag<string>()
                                        let mutable FeedBag : ConcurrentBag<string> = new ConcurrentBag<string>()
                                        let mutable FollowersBag : ConcurrentBag<string> = new ConcurrentBag<string>()
                                        let mutable SubscriptionBag : ConcurrentBag<string> = new ConcurrentBag<string>()
                                        let mutable UsersTableUpdated : bool = false
                                        let mutable TweetTableUpdated : bool = false
                                        let mutable FeedTableUpdated : bool = false
                                        let mutable FollowersTableUpdated : bool = false
                                        let mutable SubscriptionTableUpdated : bool = false
                                        
                                        if not (userTable.ContainsKey(username)) then
                                           UsersTableUpdated <- userTable.TryAdd(username, password)
                                        else
                                           UsersTableUpdated <- true

                                        if not (tweetTable.ContainsKey(username)) then
                                           TweetTableUpdated <- (tweetTable.TryAdd(username , TweetBag)) 
                                        else
                                           TweetTableUpdated <- true

                                        if not (feedTable.ContainsKey(username)) then
                                           FeedTableUpdated <- (feedTable.TryAdd(username , FeedBag)) 
                                        else
                                           FeedTableUpdated <- true

                                        if not (followersTable.ContainsKey(username)) then
                                           FollowersTableUpdated <- (followersTable.TryAdd(username, FollowersBag))
                                        else
                                           FollowersTableUpdated <- true

                                        if not (subscriptionTable.ContainsKey(username)) then
                                           SubscriptionTableUpdated <- (subscriptionTable.TryAdd(username, SubscriptionBag))
                                        else
                                           SubscriptionTableUpdated <- true

                                        SuccessfulRegistration <- (UsersTableUpdated && TweetTableUpdated && FeedTableUpdated && FollowersTableUpdated && SubscriptionTableUpdated)

                                    let mutable dataString : string = "user - " + username + " is now registered with Twitter"
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "RegisterUser" ; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson
                                    
                                 
                          

     | :? Login as msg -> let (int1, int2, int3, username , password) = unbox<Login> msg
                          let mutable doesUsernameExists: bool = userTable.ContainsKey(username)
                          if (doesUsernameExists) then
                             if(password = (userTable.Item(username))) then
                                UpUsers.Add(username)
                                printfn "user %s has logged successfully"  username
                                let mutable dataString : string = "User - " + username + "has successfully logged in"
                                let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Login"; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                let responseJson : string = Json.serialize responseJsonObj
                                x.Sender <! responseJson
                             else
                                let mutable dataString : string = "Login unsuccessfull" + username + "invalid password"
                                let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Login"; RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                let responseJson : string = Json.serialize responseJsonObj
                                x.Sender <! responseJson
                          else
                                let mutable dataString : string = "LogIn unsuccessfull user - " + username + " does not exist"
                                let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Login"; RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                let responseJson : string = Json.serialize responseJsonObj
                                x.Sender <! responseJson

     | :? Follow as msg -> let mutable (int1, username , followUserId ) = unbox<Follow> msg
                           if (UpUsers.Contains(username)) then
                              let mutable isUserFollowed : bool = false
                              let mutable loopBreakFlag : bool = true
                              while (not isUserFollowed) && loopBreakFlag do
                                    if(followersTable.ContainsKey(followUserId)) then
                                        let mutable followerBag : ConcurrentBag<string> = followersTable.Item(followUserId)
                                        let mutable bagContentList = ((followerBag.ToArray()) |> Array.toList)  
                                        if not (List.contains (username) (bagContentList) ) then
                                           followerBag.Add(username)
                                           isUserFollowed <- true
                                        else
                                           isUserFollowed <- true
                                    else
                                         loopBreakFlag <- false //to break the loop

                              let mutable isUserAddedTosubscriptionList : bool = false
                              let mutable loopBreakFlag2 : bool = true
                              while (not isUserAddedTosubscriptionList) && loopBreakFlag2 do
                                    if(subscriptionTable.ContainsKey(username)) then
                                        let mutable SubscriptionBag : ConcurrentBag<string> = subscriptionTable.Item(username)
                                        let mutable bagContentListofSubscriptions = ((SubscriptionBag.ToArray()) |> Array.toList)  
                                        if not (List.contains (followUserId) (bagContentListofSubscriptions) ) then
                                           SubscriptionBag.Add(followUserId)
                                           isUserAddedTosubscriptionList <- true
                                        else
                                           isUserAddedTosubscriptionList <- true
                                    else
                                         loopBreakFlag <- false //to break the loop

                              if (isUserFollowed && isUserAddedTosubscriptionList) then
                                 let mutable dataString : string = "User " + username + "now followes user - " + followUserId 
                                 let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Follow" ; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                 let responseJson : string = Json.serialize responseJsonObj
                                 x.Sender <! responseJson
                              else
                                    let mutable dataString : string = "user - " + username + "tried to follow user : " + followUserId + " : operation failed"
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Follow" ;RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson

                           else 
                               let mutable dataString : string = "User -" + username + " Please login to follow other users."
                               let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Follow" ; RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                               let responseJson : string = Json.serialize responseJsonObj
                               x.Sender <! responseJson

                          

     | :? Logout as msg -> let (int1, int2, int3, int4, username , password) = unbox<Logout> msg
                           if (UpUsers.Contains(username)) then 
                              UpUsers.Remove(username) |> ignore
                              let mutable dataString : string =  "User - " + username + "Logged out" 
                              let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Logout";RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                              let responseJson : string = Json.serialize responseJsonObj
                              x.Sender <! responseJson
                           else
                              let mutable dataString : string =  "Logout Failed " + username + "please login"
                              let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Logout";RequestStatus = "FAILED" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                              let responseJson : string = Json.serialize responseJsonObj
                              x.Sender <! responseJson


     | :? Tweet as msg -> let mutable (tweetNumber , int2 , username , tweet , hashtag , mention) = unbox<Tweet> msg 
                          if (UpUsers.Contains(username)) then                              
                             let mutable tweetBagEmptyObj : ConcurrentBag<string> = new ConcurrentBag<string>()
                             let mutable feedBagEmptyObj : ConcurrentBag<string> = new ConcurrentBag<string>()

                             let mutable isTweetSuccessful : bool = false
                             let mutable TweetTableUpdated : bool = false
                             let mutable FeedTableUpdated : bool = false
                             let mutable isHashTagTableUpdated : bool = false
                             let mutable isMentionTableUpdated : bool = false
                             
                                //add to tweet table
                             let mutable tweetsBag : ConcurrentBag<string> = tweetTable.Item(username)
                             tweetsBag.Add("Tweet - ["+ tweet + "]   Tweet Time - " + (DateTime.Now.ToString()))
                             let mutable dataString : string = "User" + username + " Tweeted "  + " Tweet " + (string tweetNumber) + " - " + tweet
                             let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Tweet"; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                             let responseJson : string = Json.serialize responseJsonObj
                             x.Sender <! responseJson
                             printfn "tweet -  %s" responseJson 
                             let mutable FollowersBag : ConcurrentBag<string> = followersTable.Item(username)
                             let mutable followersArray : string array = FollowersBag.ToArray()
                             let mutable feedTweet : string = " user : " + username + " Tweeted - " + tweet
                             for i in 0 .. (followersArray.Length - 1) do
                                    //feed table of all  followers
                                    let mutable FeedBag : ConcurrentBag<string> = feedTable.Item(followersArray.[i])
                                    
                                    FeedBag.Add(feedTweet)
                             if (mentionsTable.ContainsKey(mention) ) then
                                    let mutable mentionsBag : ConcurrentBag<string> = mentionsTable.Item(mention)
                                    mentionsBag.Add(feedTweet)
                             else
                                    let mutable mentionBagObj : ConcurrentBag<string> = new ConcurrentBag<string>()
                                    mentionBagObj.Add(feedTweet)
                                    let mutable tryAddFlag2 : bool = false
                                    while (not tryAddFlag2) do
                                        tryAddFlag2 <-  mentionsTable.TryAdd(mention,mentionBagObj)

                             if (hashtagTable.ContainsKey(hashtag) ) then
                                    let mutable hashTagsBag : ConcurrentBag<string> = hashtagTable.Item(hashtag)
                                    hashTagsBag.Add(feedTweet)
                             else
                                    let mutable hashTagsBagObj : ConcurrentBag<string> = new ConcurrentBag<string>()
                                    hashTagsBagObj.Add(feedTweet)
                                    let mutable tryAddFlag3 : bool = false
                                    while (not tryAddFlag3) do
                                        tryAddFlag3 <-  hashtagTable.TryAdd(hashtag,hashTagsBagObj)  
                                                            
     | :? Retweet as msg -> let mutable (int1,int2,int3,int4,int5, ActualTweeter , Retweeter , tweet) = unbox<Retweet> msg
                            let mutable SubscriptionBag : ConcurrentBag<string> = subscriptionTable.Item(Retweeter)
                            let mutable followedTweeters : string array = SubscriptionBag.ToArray()
                            let mutable retweetfound : bool = false
                            let mutable i = 0
                            while i < (followedTweeters.Length - 1) && (not retweetfound) do
                                    if(feedTable.ContainsKey(followedTweeters.[i])) then
                                        let mutable FeedBag : ConcurrentBag<string> = feedTable.Item(followedTweeters.[i])
                                        let mutable feedTweetArray : string array = FeedBag.ToArray()
                                        if(feedTweetArray.Length > 0) then
                                            retweetfound < true
                                            let mutable index = System.Random().Next(feedTweetArray.Length)
                                            let mutable dataString : string = "User - " + Retweeter + " Original retweet : " + feedTweetArray.[index]
                                            let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Retweet"; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                            let responseJson : string = Json.serialize responseJsonObj
                                            x.Sender <! responseJson
                                    i <- i + 1
                            
                            if not (retweetfound ) then
                                        let mutable dataString : string = "User " + Retweeter + "Actual tweet " + ActualTweeter
                                        let mutable responseJsonObj : ResponseJsonObj = {RequestType = "Retweet"; RequestStatus = "SUCCESS" ; ResponseString1 = dataString ; ResponseString2 = "none" }
                                        let responseJson : string = Json.serialize responseJsonObj
                                        x.Sender <! responseJson

     | :? HashTagQ as msg -> let mutable (int1, int2, int3, hashtag) =  unbox<HashTagQ> msg
                             if (hashtagTable.ContainsKey(hashtag)) then
                                    let mutable hashTagsBag : ConcurrentBag<string> = hashtagTable.Item(hashtag)
                                    let mutable hashTaggedTweets : string array =  hashTagsBag.ToArray()
                                    let mutable allTweetsContainingHashtag : string = ""
                                    for i in 0 .. hashTaggedTweets.Length - 1 do
                                        allTweetsContainingHashtag <- allTweetsContainingHashtag + " , " + hashTaggedTweets.[i]
                                    //x.Sender <! (true ,hashtag, allTweetsContainingHashtag)
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "HashTagQ" ; RequestStatus = "SUCCESS" ; ResponseString1 = hashtag ; ResponseString2 = allTweetsContainingHashtag }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson
                             else
                                    //x.Sender <! (false ,hashtag, "")
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "HashTagQ" ; RequestStatus = "FAILED" ; ResponseString1 = hashtag ; ResponseString2 = "none" }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson


     | :? MentionQ as msg -> let mutable (int1, int2, int3, int4, mention) =  unbox<MentionQ> msg
                             if (mentionsTable.ContainsKey(mention)) then
                                    let mutable mentionsBag : ConcurrentBag<string> = mentionsTable.Item(mention)
                                    let mutable mentionTweets : string array =  mentionsBag.ToArray()
                                    let mutable allTweetsContainingMentions : string = ""
                                    for i in 0 .. mentionTweets.Length - 1 do
                                        allTweetsContainingMentions <- allTweetsContainingMentions + " , " + mentionTweets.[i]
                                    //x.Sender <! (true ,true, mention ,allTweetsContainingMentions)
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "MentionsQ" ; RequestStatus = "SUCCESS" ; ResponseString1 = mention ; ResponseString2 = allTweetsContainingMentions }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson
                             else
                                    //x.Sender <! (false , false, mention ,"")      
                                    let mutable responseJsonObj : ResponseJsonObj = {RequestType = "MentionsQ" ; RequestStatus = "FAILED" ; ResponseString1 = mention ; ResponseString2 = "none" }
                                    let responseJson : string = Json.serialize responseJsonObj
                                    x.Sender <! responseJson

     | :? GetUserTweets as msg -> let mutable (int1, int2, int3, int4, int5 , userId) =  unbox<GetUserTweets> msg 
                                  if (tweetTable.ContainsKey(userId)) then
                                         let mutable tweetsBag : ConcurrentBag<string> = tweetTable.Item(userId)
                                         let mutable tweetsArray : string array =  tweetsBag.ToArray()
                                         let mutable allTweetsFromUser: string = ""
                                         for i in 0 .. tweetsArray.Length - 1 do
                                            allTweetsFromUser <- allTweetsFromUser + " , " + tweetsArray.[i]
                                         //x.Sender <! (true ,true, true, userId , allTweetsFromUser)
                                         let mutable responseJsonObj : ResponseJsonObj = {RequestType = "GetUsersTweetQuery" ; RequestStatus = "SUCCESS" ; ResponseString1 = userId ; ResponseString2 = allTweetsFromUser }
                                         let responseJson : string = Json.serialize responseJsonObj
                                         x.Sender <! responseJson
                                  else
                                         //x.Sender <! (true ,true, true, userId , "")
                                         let mutable responseJsonObj : ResponseJsonObj = {RequestType = "GetUsersTweetQuery" ; RequestStatus = "FAILED" ; ResponseString1 = userId ; ResponseString2 = "none" }
                                         let responseJson : string = Json.serialize responseJsonObj
                                         x.Sender <! responseJson
                                                                          

     | _ -> failwith "invalid message Format"

//_________________________________________****************************************************__________________________________________________***************************************************************************************____________


let serverActorRefArray : IActorRef array = Array.zeroCreate (ServerInstances+1)
for i in 1 .. ServerInstances do
    let mutable serverId : string = "server" + string(i)
    serverActorRefArray.[i - 1] <- system.ActorOf(Props(typedefof<ServerActor>),serverId)
    printfn "server created with serverId %s" serverId

//__________________________________________________________*****************************************************_______________________________________

let ws (webSocket : WebSocket) (context: HttpContext) =
  socket {
    let mutable loop = true

    while loop do
      let! msg = webSocket.read()
      

      match msg with

      | (Text, data, true) ->
        let requestMessage = UTF8.toString data
        printfn "response - "

        let mutable serverId : int = getServerID()
        let mutable ServerReference = serverActorRefArray.[serverId - 1]
        let mutable WebSocketInMap : bool = false
        let mutable task = Unchecked.defaultof<_>
        let mutable requestJsonobj : RequestJsonObj =  Json.deserialize<RequestJsonObj>(requestMessage)
        if(requestJsonobj.RequestType = "RegisterUser") then
            if not (WebSocketTable.ContainsKey(requestJsonobj.UserId)) then
                let mutable WebSocketInMap : bool = false
                while not (WebSocketInMap ) do
                    WebSocketInMap <- WebSocketTable.TryAdd(requestJsonobj.UserId, webSocket)
        if(requestJsonobj.RequestType = "RegisterUser") then
            task <- ServerReference <? (requestJsonobj.UserId , requestJsonobj.Password)
        if(requestJsonobj.RequestType = "Login") then
            task <- ServerReference <? (1,1,1,requestJsonobj.UserId , requestJsonobj.Password)
        if(requestJsonobj.RequestType = "Follow") then
            task <- ServerReference <? (1,requestJsonobj.UserId , requestJsonobj.FollowUserId)
        if(requestJsonobj.RequestType = "Tweet") then
            printfn("tweeting now")
            task <- ServerReference <? (requestJsonobj.TweetNumber, 1, requestJsonobj.UserId , requestJsonobj.Tweet,requestJsonobj.HashTag ,requestJsonobj.Mention)
        if(requestJsonobj.RequestType = "Retweet") then
            task <- ServerReference <? (1, 1, 1, 1, 1, requestJsonobj.SubscriptionUserName, requestJsonobj.UserId ,requestJsonobj.Tweet)
        if(requestJsonobj.RequestType = "HashTagQ") then 
            task <- ServerReference <? (1, 1, 1, requestJsonobj.HashTag)  
        if(requestJsonobj.RequestType = "MentionsQ") then
            task <- ServerReference <?  (1, 1, 1, 1, requestJsonobj.Mention) 
        if(requestJsonobj.RequestType = "GetUsersTweetQuery") then
            task <-  ServerReference <? (1, 1, 1, 1, 1 , requestJsonobj.UserId)
        if(requestJsonobj.RequestType = "Logout") then
            task <- ServerReference <? (1,1,1,1,requestJsonobj.UserId , requestJsonobj.Password)

        
        let responseActor = Async.RunSynchronously (task, 30000)
        let mutable response = sprintf "%s" responseActor
        printfn "Actor response - %s" response
        if(response = Unchecked.defaultof<_> || response.Length = 0 ) then
            let mutable responseJsonObj : ResponseJsonObj = {RequestType = requestJsonobj.RequestType ; RequestStatus = "FAILED" ; ResponseString1 = "Server failed to serve the request because of high load" ; ResponseString2 = "none" }
            printfn "change the response due to request failure"
            response <- Json.serialize responseJsonObj
        printfn "getuserstweetsquery response ws is : %s" response


        // converting the response into bite segments
        let byteResponse =
          response
          |> System.Text.Encoding.ASCII.GetBytes
          |> ByteSegment

        // client gets the messgae back from send method 
        do! webSocket.send Text byteResponse true

      | (Close, _, _) ->
        let emptyResponse = [||] |> ByteSegment
        do! webSocket.send Close emptyResponse true

        // closing the loop
        loop <- false

      | _ -> ()
    }

//_________________________________________________*****************************************************____________________________________________________________

/// fetching and handeling the websocket error 
let wsWithErrorHandling (webSocket : WebSocket) (context: HttpContext) = 
   
   let DisposableResource = { new IDisposable with member __.Dispose() = printfn "Resource for teh websocket connection has been disposed" }
   let websocketWorkflow = ws webSocket context
   
   async {
    let! successOrError = websocketWorkflow
    match successOrError with
    | Choice1Of2() -> ()
    // fail case
    | Choice2Of2(error) ->
        printfn "Error: [%A]" error
        DisposableResource.Dispose()
        
    return successOrError
   }


//________________________________________*********************************************************______________________________________________

let app : WebPart = 
  choose [
    path "/websocket" >=> handShake ws
    path "/websocketWithSubprotocol" >=> handShakeWithSubprotocol (chooseSubprotocol "test") ws
    path "/websocketWithError" >=> handShake wsWithErrorHandling
    GET >=> choose [ path "/" >=> OK "index" ]
    NOT_FOUND "Found no handlers." ]


startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
printfn "starting the server"


