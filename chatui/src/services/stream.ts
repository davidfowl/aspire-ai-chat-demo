import { MessageFragment } from "../types/ChatTypes";

async function* streamJsonValues(response: Response, signal: AbortSignal): AsyncGenerator<MessageFragment> {
    const reader: ReadableStreamDefaultReader<Uint8Array> = response.body!.getReader();
    const decoder = new TextDecoder();
    let buffer = "";
    let pos = 0;
    let inArray = false;      // Are we processing a top-level array?
    let startedArray = false; // Have we seen the initial '['?

    // Helper: skip whitespace starting at index `start` in string `str`.
    function skipWhitespace(str: string, start: number): number {
        while (start < str.length && /\s/.test(str[start])) {
            start++;
        }
        return start;
    }

    // Handle the abort event
    signal.addEventListener('abort', () => {
        reader.cancel();
    });

    // Process incoming chunks repeatedly.
    while (true) {
        if (signal.aborted) {
            throw new DOMException('Aborted', 'AbortError');
        }

        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });

        pos = 0;
        // Process as many complete JSON values as possible.
        while (pos < buffer.length) {
            pos = skipWhitespace(buffer, pos);
            if (pos >= buffer.length) break;

            // If we are in an array and we see a comma, skip it.
            if (inArray && buffer[pos] === ",") {
                pos++;
                continue;
            }

            // If we haven’t seen the opening of an array yet,
            // and the first non-whitespace character is '[',
            // then we are in array mode.
            if (!startedArray && buffer[pos] === "[") {
                inArray = true;
                startedArray = true;
                pos++; // skip the '['
                continue;
            }

            // If in array mode, a closing ']' means the stream is finished.
            if (inArray && buffer[pos] === "]") {
                pos++;
                buffer = buffer.slice(pos);
                pos = 0;
                return; // end generator
            }

            // At this point we are at the start of a new JSON value.
            const valueStart = pos;
            let end: number | null = null;
            const firstChar = buffer[pos];

            // For objects or arrays, use a stack to find the matching close.
            if (firstChar === "{" || firstChar === "[") {
                let stack: string[] = [firstChar];
                pos++;
                let inString = false;
                let escapeCount = 0;
                while (pos < buffer.length) {
                    const ch = buffer[pos];
                    if (inString) {
                        if (ch === '"' && escapeCount % 2 === 0) {
                            inString = false;
                        } else if (ch === "\\") {
                            escapeCount++;
                        } else {
                            escapeCount = 0;
                        }
                    } else {
                        if (ch === '"') {
                            inString = true;
                            escapeCount = 0;
                        } else if (ch === "{" || ch === "[") {
                            stack.push(ch);
                        } else if (ch === "}" || ch === "]") {
                            const last = stack[stack.length - 1];
                            if ((last === "{" && ch === "}") || (last === "[" && ch === "]")) {
                                stack.pop();
                                if (stack.length === 0) {
                                    pos++; // include closing bracket
                                    end = pos;
                                    break;
                                }
                            }
                        }
                    }
                    pos++;
                }
                // If we haven't found a complete structure, wait for more data.
                if (end === null) break;
            }
            // If the value is a string literal.
            else if (firstChar === '"') {
                pos++; // skip opening quote
                let inString = true;
                let escapeCount = 0;
                while (pos < buffer.length) {
                    const ch = buffer[pos];
                    if (ch === '"' && escapeCount % 2 === 0) {
                        inString = false;
                        pos++; // include closing quote
                        break;
                    }
                    if (ch === "\\") {
                        escapeCount++;
                    } else {
                        escapeCount = 0;
                    }
                    pos++;
                }
                if (inString) {
                    // Incomplete string—wait for more data.
                    break;
                }
                end = pos;
            }
            // Otherwise, it might be a number, boolean, or null.
            else {
                // Scan until a delimiter: comma, closing bracket, or whitespace.
                while (pos < buffer.length && !/[,\]\s]/.test(buffer[pos])) {
                    pos++;
                }
                end = pos;
            }

            // If we haven’t determined a complete value, wait for more data.
            if (end === null) break;

            let jsonStr = buffer.substring(valueStart, end).trim();
            // If in an array, a trailing comma might be present. Remove it.
            if (jsonStr.endsWith(",")) {
                jsonStr = jsonStr.slice(0, -1);
            }

            try {
                const parsed = JSON.parse(jsonStr);
                yield parsed;
            } catch (e) {
                // Likely an incomplete value—wait for more data.
                break;
            }

            // After a value, skip any following whitespace and an optional comma.
            pos = skipWhitespace(buffer, pos);
            if (buffer[pos] === ",") {
                pos++;
            }
        }
        // Remove processed text from the buffer.
        buffer = buffer.slice(pos);
        pos = 0;
    }

    // At stream end, attempt one final parse if any non-whitespace remains.
    buffer = buffer.trim();
    if (buffer.startsWith(",")) {
        buffer = buffer.slice(1).trim();
    }
    if (buffer) {
        try {
            yield JSON.parse(buffer) as MessageFragment;
        } catch (e) {
            console.warn("Incomplete JSON value discarded:", buffer);
        }
    }
}

export { streamJsonValues };
