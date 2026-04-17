import {FilePdf} from "../class/FilePdf.tsx";

interface FilesListItemProps {
    file: FilePdf;
}

function FilesListItem({file}: FilesListItemProps) {
    return (
        <li>
            {file.filename}
        </li>
    );
}

export default FilesListItem;