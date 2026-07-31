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
        case 'RequestBusinessReview':
            return 'darkorange';
        case 'CompleteBusinessReview':
            return 'purple';
        default:
            return 'inherit';
    }
};

export const getVerificationTypeBackgroundColor = (type: string): string => {
    switch (type) {
        case 'Confirmed':
            return 'inherit';
        case 'AutoConfirm':
            return 'green';
        case 'Removed':
            return 'inherit';
        case 'AutoFail':
            return 'red';
        case 'Edited':
            return 'inherit';
        case 'AutoWarn':
            return 'darkorange';
        case 'Added':
            return 'inherit';
        case 'RequestBusinessReview':
            return 'darkorange';
        case 'CompleteBusinessReview':
            return 'purple';
        default:
            return 'inherit';
    }
};

export const getVerificationTypeInitials = (type: string): string => {
    switch (type) {
        case 'Confirmed':
            return '✅';
        case 'AutoConfirm':
            return 'AC';
        case 'Removed':
            return '❌';
        case 'Edited':
            return '✏️';
        case 'Added':
            return '➕';
        case 'AutoFail':
            return 'AF';
        case 'AutoWarn':
            return 'AW';
        case 'RequestBusinessReview':
            return 'RBR';
        case 'CompleteBusinessReview':
            return 'CBR';
        default:
            return '';
    }
};
