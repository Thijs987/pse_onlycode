extends Node2D

@onready var GSCWS = $"../GSCWS"
@onready var GSCHTTP = $"../GSCHTTP"

# In order to make use of this signal put
# "Controller.message_updated.connect(_on_message)" in _ready()
# Then create the function "_on_message" or any other name as long as it
# Matches what is inbetween the brackets. 
# This function will get access to the message sent by the server
signal message_updated(msg)

var PId := ""

var Last_Message := {}
var Last_Data := {}
var Active_Lobbies := []
var Player_Hand := []

func Get_Lobbies():
	GSCHTTP.Get_Lobbies()

func Create_Lobby(Player_Id: String):
	PId = Player_Id
	GSCHTTP.Create_Lobby(PId)

func Join_Lobby(Lobby_Id: String, Player_Id: String):
	PId = Player_Id
	GSCWS.Join_Lobby(Lobby_Id, PId)

func Play_Card(Player_Id: String, card_id: String):
	PId = Player_Id
	GSCWS.Play_Card(PId, card_id)

func Draw_Card(Player_Id: String):
	PId = Player_Id
	GSCWS.Draw_Card(PId)

func Start_Match(Player_Id: String):
	PId = Player_Id
	GSCWS.Start_Match(PId)

# From_Hand is a bool that describes whether the given card comes from a player hand
func Gift_Card(Opponent_Id: String, CardId: String, From_Hand: bool):
	if From_Hand == true:
		Player_Hand.erase(CardId)
	
	GSCWS.Gift_Card(Opponent_Id, CardId, From_Hand)

# Ask for data 
# with Controller.Last_Message["action"]
# or Controller.Last_Message["data"]["cardId"]
func Update_From_Server(msg: Dictionary):
	Last_Message = msg
	Last_Data = msg["data"]
	
	if Last_Message["action"] == "DRAW_CARD":
		Player_Hand.append(Last_Message["data"]["cardId"])
	
	if Last_Message["action"] == "CARD_PLAYED":
		Player_Hand.erase(Last_Message["data"]["cardId"])
	
	message_updated.emit(msg)


func Update_Lobbies(lobbies: Array):
	Active_Lobbies = lobbies


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
