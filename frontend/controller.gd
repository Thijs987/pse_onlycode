extends Node2D

var GSCHTTP
@onready var GSCWS = $"../GSCWS"


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	GSCHTTP = $"../GSCHTTP"
	Create_Lobby("Player_1")
	
func Create_Lobby(PId: String):
	GSCHTTP.Create_Lobby(PId)
	
func Play_Card(PId: String, card_id: String):
	GSCWS.Play_Card(PId, card_id)
	
func Draw_Card(PId: String):
	GSCWS.Draw_Card(PId)



# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
