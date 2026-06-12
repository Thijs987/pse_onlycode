extends Node2D

# In order to make use of this signal put
# "Controller.message_updated.connect(_on_message)" in _ready()
# Then create the function "_on_message" or any other name as long as it
# Matches what is inbetween the brackets. 
# This function will get access to the message sent by the server
signal message_updated(msg)

var PId := "" #SHOULD CHANGE THIS BACK TO ""

var Last_Message := {}
var Last_Data := {}
var Active_Lobbies := []
var Player_Hand := []


func Get_Lobbies():
	gschttp.Get_Lobbies()

func Create_Lobby(Player_Id: String):
	PId = Player_Id
	gschttp.Create_Lobby(PId)

func Join_Lobby(Lobby_Id: String, Player_Id: String):
	PId = Player_Id
	gscws.Join_Lobby(Lobby_Id, PId)

func Play_Card(Player_Id: String, card_id: String):
	PId = Player_Id
	gscws.Play_Card(PId, card_id)

func Draw_Card(Player_Id: String):
	PId = Player_Id
	gscws.Draw_Card(PId)

func Start_Match(Player_Id: String):
	PId = Player_Id
	gscws.Start_Match(PId)

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
	print(Last_Message)
	Last_Data = msg["data"]
	
	if Last_Message["action"] == "CARD_DRAWN":
		Player_Hand.append(Last_Message["data"]["cardId"])
	
	if Last_Message["action"] == "CARD_PLAYED":
		Player_Hand.erase(Last_Message["data"]["cardId"])
	
	if Last_Message["action"] == "MATCH_STARTED":
		Player_Hand = Last_Message["data"]["cards"]

	message_updated.emit(msg)


func Update_Lobbies(lobbies: Array):
	Active_Lobbies = lobbies


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
