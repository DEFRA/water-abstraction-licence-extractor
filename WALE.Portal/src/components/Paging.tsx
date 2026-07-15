import { useEffect, useState } from 'react';

type PagingProps = {
    pageNumber: number;
    totalPages: number;
    totalLicences: number;
    pageSize: number;
    searchTerm: string;
    setPageNumber: (pageNumber: number) => void;
    setPageSize: (pageSize: number) => void;
    setSearchTerm: (searchTerm: string) => void;
};

export default function Paging({
                                   pageNumber,
                                   totalPages,
                                   totalLicences,
                                   pageSize,
                                   searchTerm,
                                   setPageNumber,
                                   setPageSize,
                                   setSearchTerm
                               }: PagingProps) {
    const [searchText, setSearchText] = useState(searchTerm);

    useEffect(() => {
        const handler = setTimeout(() => {
            const trimmedValue = searchText.trim();
            if (trimmedValue === searchTerm) return;

            if (trimmedValue.length === 0 || trimmedValue.length > 3) {
                setSearchTerm(trimmedValue);
                setPageNumber(1);
            }
        }, 1000);

        return () => {
            clearTimeout(handler);
        };
    }, [searchText, searchTerm, setSearchTerm, setPageNumber]);

    const pages = Array.from({ length: totalPages }, (_, index) => index + 1);

    const handlePageSizeChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
        const newPageSize = Number(e.target.value);

        setPageSize(newPageSize);
        setPageNumber(1);
    };

    const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setSearchText(e.target.value);
    };

    return (
        <div
            style={{
                clear: 'both',
                display: 'block',
                width: '100%',
                marginTop: '10px',
                marginLeft: '250px'
            }}
        >
            <label>
                Search:{' '}
                <input
                    type="text"
                    value={searchText}
                    onChange={handleSearchChange}
                    placeholder="Search..."
                    style={{ width: '180px' }}
                />
            </label>

            {searchText.trim().length > 0 && searchText.trim().length <= 3 && (
                <span style={{ marginLeft: '8px' }}>
                    Enter more than 3 characters
                </span>
            )}

            {totalPages > 0 && (
                <>
                    &nbsp;&nbsp;&nbsp;

                    {totalLicences} licence(s) found :  Page {pageNumber} of {totalPages}&nbsp;&nbsp;&nbsp;

                    <label>
                        Page size:{' '}
                        <select
                            value={pageSize}
                            onChange={handlePageSizeChange}
                            style={{ width: '60px' }}
                        >
                            <option value={10}>10</option>
                            <option value={100}>100</option>
                            <option value={500}>500</option>
                            <option value={1000}>1000</option>
                        </select>
                    </label>

                    &nbsp;&nbsp;&nbsp;

                    {pageNumber > 1 && (
                        <>
                            <a
                                href="#"
                                onClick={(e) => {
                                    e.preventDefault();
                                    setPageNumber(pageNumber - 1);
                                }}
                            >
                                Prev
                            </a>
                            {' | '}
                        </>
                    )}

                    {pages.map((page) => (
                        <span key={page}>
                            {page === pageNumber ? (
                                <strong>{page}</strong>
                            ) : (
                                <a
                                    href="#"
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setPageNumber(page);
                                    }}
                                >
                                    {page}
                                </a>
                            )}
                            {' '}
                        </span>
                    ))}

                    {totalPages > pageNumber && (
                        <>
                            {' | '}
                            <a
                                href="#"
                                onClick={(e) => {
                                    e.preventDefault();
                                    setPageNumber(pageNumber + 1);
                                }}
                            >
                                Next
                            </a>
                        </>
                    )}
                </>
            )}
        </div>
    );
}