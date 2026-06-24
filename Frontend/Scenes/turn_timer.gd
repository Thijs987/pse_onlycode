extends Timer

@onready var time_label: Label = $"../Background/TimeLabel"

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	var time = self.time_left
	time_label.text = str(round(time))
