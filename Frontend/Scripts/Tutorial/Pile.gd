extends Node2D

@onready var hand_reference = $"../PlayerHand"
@onready var draw_sound := AudioStreamPlayer.new()

signal card_drawn()

func _ready():
	add_child(draw_sound)
	draw_sound.stream = preload("res://Sounds/PlayCard.mp3")

# Haalt 1 kaart van de counter
func draw_card():
	if controller.interaction_disabled:
		return
	draw_sound.play()
	card_drawn.emit()
