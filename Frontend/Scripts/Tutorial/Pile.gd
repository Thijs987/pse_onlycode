extends Node2D

@onready var hand_reference = $"../PlayerHand"
@onready var leader = $"../Leader"

signal card_drawn()

# Haalt 1 kaart van de counter
func draw_card():
	if controller.interaction_disabled:
		return
	if leader.turns == controller.PId:
		card_drawn.emit()
