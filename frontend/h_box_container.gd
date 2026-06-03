extends HBoxContainer

@onready var Card: PackedScene = preload("res://card.tscn") 


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	var Card1 = Card.instantiate()
	add_child(Card1)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
