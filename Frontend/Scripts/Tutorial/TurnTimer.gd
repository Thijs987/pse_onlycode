extends Timer

@onready var time_label: Label = $"../Background/TimeLabel"

func _ready():
	pass

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	var time = self.time_left
	time_label.text = str(round(time))
