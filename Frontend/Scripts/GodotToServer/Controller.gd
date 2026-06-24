extends Node2D

@onready var LobbySettings = $"../LobbySettings"

# In order to make use of this signal put
# "Controller.message_updated.connect(_on_message)" in _ready()
# Then create the function "_on_message" or any other name as long as it
# Matches what is inbetween the brackets.
# This function will get access to the message sent by the server
signal message_updated(msg)
signal lobbies_updated(lobbies)
signal lobby_join_failed()
signal lobby_left()

var PId := ""
var player_list = ["", "", "", ""]

var Last_Message := {}
var Last_Data := {}
var Active_Lobbies := []
var Player_Hand := []
var All_Player_Ids := []
var Hand_Sizes := {}
var custom_set = {"CM": 4,
				 "Ddos": 2,
				 "SQL": 2,
				 "MS": 2,
				 "Err": 4,
				 "Goto": 4,
				 "IH": 4,
				 "Unplayable": 6,
				 "TH": 4,
				 "OpS": 2
				 }

var interaction_disabled := false


signal rejoin_lobbies_updated(lobbies)

func Get_Lobbies():
	gschttp.Get_Lobbies()

func Get_Rejoin_Lobbies(Player_Id: String):
	PId = Player_Id
	gschttp.Get_Rejoin_Lobbies(PId)

func Abandon_Lobby(Lobby_Id: String, Player_Id: String):
	PId = Player_Id
	gschttp.Abandon_Lobby(Lobby_Id, PId)

func Update_Rejoin_Lobbies(lobbies: Array):
	rejoin_lobbies_updated.emit(lobbies)

func Create_Lobby(Player_Id: String):
	interaction_disabled = false
	PId = Player_Id
	gschttp.Create_Lobby(PId)

func Join_Lobby(Lobby_Id: String, Player_Id: String):
	interaction_disabled = false
	PId = Player_Id
	gscws.Join_Lobby(Lobby_Id, PId)

func Leave_Lobby():
	interaction_disabled = true
	gscws.Leave_Lobby()

#func Play_Card(Player_Id: String, Arr: Array,  OId: String):
	#PId = Player_Id
	#gscws.Play_Card(Arr, OId)

func Play_Card(Player_Id: String, data: Dictionary = {}):
	PId = Player_Id
	gscws.Play_Card(data)

func Draw_Card(Player_Id: String):
	PId = Player_Id
	gscws.Draw_Card()

func Start_Match(Player_Id: String):
	interaction_disabled = false
	PId = Player_Id
	gscws.Start_Match(custom_set)

func Add_Bot():
	gscws.Add_Bot()

func Kick_Player(target_id: String):
	gscws.Kick_Player(target_id)

# From_Hand is a bool that describes whether the given card comes from a player hand
func Gift_Card(Opponent_Id: String, CardId: String, From_Hand: bool):
	if From_Hand == true:
		Player_Hand.erase(CardId)

	gscws.Gift_Card(Opponent_Id, CardId, From_Hand)

# Ask for data
# with Controller.Last_Message["action"]
# or Controller.Last_Message["data"]["cardId"]
func Update_From_Server(msg: Dictionary):
	Last_Message = msg
	print("Player: " + str(PId) + ", " + str(Last_Message))
	Last_Data = msg.get("data", {})

	if Last_Message.get("action") == "PLAYER_JOINED" or Last_Message.get("action") == "PLAYER_REJOINED":
		var joined_id = Last_Message.get("playerId")
		if joined_id and not All_Player_Ids.has(joined_id):
			All_Player_Ids.append(joined_id)

	if Last_Message.get("action") == "CARD_DRAWN":
		if Last_Data.has("cardId") and Last_Data["cardId"] != null:
			Player_Hand.append(Last_Data["cardId"])

	if Last_Message.get("action") == "CARD_PLAYED":
		if Last_Data.has("cardId"):
			Player_Hand.erase(Last_Data["cardId"])

		if Last_Data.has("cards") && Last_Data.get("cards").size() == 3:
				Player_Hand.append(Last_Data.get("cards")[2])


	if Last_Message.get("action") == "MATCH_STARTED":
		if Last_Data.has("cards"):
			Player_Hand = Last_Data["cards"]
		if Last_Data.has("players"):
			All_Player_Ids = Last_Data["players"]
		if Last_Data.has("handSizes"):
			Hand_Sizes = Last_Data["handSizes"]

	if Last_Message.get("action") == "GAME_OVER":
		interaction_disabled = true

	if Last_Message.get("action") == "CARD_LIMIT" and msg.get("playerId") == PId:
		interaction_disabled = true

	if Last_Message.get("action") == "HAND":
		if Last_Data.has("cards"):
			Player_Hand = Last_Data["cards"]
		if Last_Data.has("players"):
			All_Player_Ids = Last_Data["players"]
		if Last_Data.has("handSizes"):
			Hand_Sizes = Last_Data["handSizes"]

	message_updated.emit(msg)


func Update_Lobbies(lobbies: Array):
	Active_Lobbies = lobbies
	lobbies_updated.emit(lobbies)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
