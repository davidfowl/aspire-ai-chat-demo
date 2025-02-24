export interface Chat {
	id: string;
	name: string;
}

export interface Message {
	id: string;
	sender: string;
	text: string;
}

export interface MessageFragment extends Message {
	fragmentId: string;
	isFinal: boolean;
}
