extends Node2D
@onready var pile_location = $Discard_pile_location


var discarded_cards = []

func add_card(card):
	discarded_cards.append(card)

	var offset = discarded_cards.size() * 3

	card.global_position = pile_location.global_position + Vector2(offset, offset)

	card.z_index = discarded_cards.size()

	print("Cards in discard pile:", discarded_cards.size())
