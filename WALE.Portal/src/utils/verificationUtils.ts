export const getVerificationTypeColor = (type: string): string => {
    switch (type) {
        case 'Confirmed':
        case 'AutoConfirm':
            return 'green';
        case 'Removed':
        case 'AutoFail':
            return 'red';
        case 'Edited':
        case 'AutoWarn':
            return 'darkorange';
        case 'Added':
            return 'blue';
        default:
            return 'inherit';
    }
};
