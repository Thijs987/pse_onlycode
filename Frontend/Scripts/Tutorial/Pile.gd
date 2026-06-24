extends Node2D

@onready var hand_reference = $"../PlayerHand"

signal card_drawn()

# Haalt 1 kaart van de counter
func draw_card():
	if controller.interaction_disabled:
		return
	card_drawn.emit()
