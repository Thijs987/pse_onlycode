extends Node2D

var GSCHTTP
@onready var GSCWS = $"../GSCWS"

var PId := ""

var Last_Message := {}
var Last_Data := {}

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	GSCHTTP = $"../GSCHTTP"
	Create_Lobby("Player_1")

func Create_Lobby(Player_Id: String):
	PId = Player_Id
	GSCHTTP.Create_Lobby(PId)

func Play_Card(Player_Id: String, card_id: String):
	PId = Player_Id
	GSCWS.Play_Card(PId, card_id)

func Draw_Card(Player_Id: String):
	PId = Player_Id
	GSCWS.Draw_Card(PId)

# Ask for data 
# with Controller.Last_Message["action"]
# or Controller.Last_Message["data"]["cardId"]
func Update_From_Server(msg: Dictionary):
	Last_Message = msg
	Last_Data = msg["data"]


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
