package com.newlife.stardust;

/**
 * Exception raised when the Stardust server reports an error or a transport failure occurs.
 */
public class StardustException extends RuntimeException {

    private static final long serialVersionUID = 1L;

    private final int code;

    /**
     * Creates an exception with a server error code.
     *
     * @param code server error code, or -1 for transport/parsing failures
     * @param message error message
     */
    public StardustException(int code, String message) {
        super(message);
        this.code = code;
    }

    /**
     * Creates an exception with a server error code and a cause.
     *
     * @param code server error code, or -1 for transport/parsing failures
     * @param message error message
     * @param cause underlying cause
     */
    public StardustException(int code, String message, Throwable cause) {
        super(message, cause);
        this.code = code;
    }

    /**
     * Returns the server error code, or -1 for transport/parsing failures.
     *
     * @return error code
     */
    public int getCode() {
        return code;
    }
}
