extends Node2D

@onready var GSCWS = $"../GSCWS"
@onready var GSCHTTP = $"../GSCHTTP"

var PId := ""

var Last_Message := {}
var Last_Data := {}
var Active_Lobbies := []

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

# Ask for data 
# with Controller.Last_Message["action"]
# or Controller.Last_Message["data"]["cardId"]
func Update_From_Server(msg: Dictionary):
	Last_Message = msg
	Last_Data = msg["data"]

func Update_Lobbies(lobbies: Array):
	Active_Lobbies = lobbies


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
