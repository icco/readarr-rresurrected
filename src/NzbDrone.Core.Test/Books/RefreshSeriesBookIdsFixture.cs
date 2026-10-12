using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Books
{
    [TestFixture]
    public class RefreshSeriesBookIdsFixture : DbTest<RefreshSeriesService, SeriesBookLink>
    {
        private AuthorMetadata _metadata;
        private Book _book;

        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IBookRepository>(Mocker.Resolve<BookRepository>());
            Mocker.SetConstant<IBookService>(Mocker.Resolve<BookService>());
            Mocker.SetConstant<ISeriesRepository>(Mocker.Resolve<SeriesRepository>());
            Mocker.SetConstant<ISeriesService>(Mocker.Resolve<SeriesService>());
            Mocker.SetConstant<ISeriesBookLinkRepository>(Mocker.Resolve<SeriesBookLinkRepository>());
            Mocker.SetConstant<ISeriesBookLinkService>(Mocker.Resolve<SeriesBookLinkService>());
            Mocker.SetConstant<IRefreshSeriesBookLinkService>(Mocker.Resolve<RefreshSeriesBookLinkService>());

            _metadata = new AuthorMetadata
            {
                ForeignAuthorId = "author-1",
                TitleSlug = "author-1",
                Name = "Alice",
                SortName = "alice",
                NameLastFirst = "Alice",
                SortNameLastFirst = "alice",
                Kca = string.Empty
            };
            Db.Insert(_metadata);

            _book = new Book
            {
                AuthorMetadataId = _metadata.Id,
                ForeignBookId = "book-1",
                TitleSlug = "book-1",
                Title = "Book One",
                CleanTitle = "bookone"
            };
            Db.Insert(_book);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void should_refresh_links_using_persisted_book_ids(bool existingLink, bool partial)
        {
            var series = new Series { ForeignSeriesId = "series-1", Title = "Series One" };
            var linkId = 0;

            if (existingLink)
            {
                Db.Insert(series);
                var link = new SeriesBookLink { SeriesId = series.Id, BookId = _book.Id, Position = "1" };
                Db.Insert(link);
                linkId = link.Id;
            }

            var remoteSeries = new Series
            {
                ForeignSeriesId = "series-1",
                Title = "Series One",
                LinkItems = new List<SeriesBookLink>
                {
                    new SeriesBookLink
                    {
                        Book = new Book { ForeignBookId = "book-1", Title = "Book One" },
                        Position = "2",
                        SeriesPosition = 2,
                        IsPrimary = true
                    },
                    new SeriesBookLink
                    {
                        Book = new Book { ForeignBookId = "book-not-in-library", Title = "Book Two" },
                        Position = "3"
                    }
                }
            };
            var author = new Author
            {
                AuthorMetadataId = _metadata.Id,
                Metadata = _metadata,
                Series = new List<Series> { remoteSeries },
                IsPartial = partial
            };

            Subject.RefreshSeriesInfo(_metadata.Id, author.Series.Value, author, false, false, null);

            var stored = AllStoredModels.Should().ContainSingle().Subject;
            stored.BookId.Should().Be(_book.Id);
            stored.SeriesId.Should().Be(Db.All<Series>().Should().ContainSingle().Subject.Id);
            stored.Position.Should().Be("2");
            stored.SeriesPosition.Should().Be(2);
            stored.IsPrimary.Should().BeTrue();

            if (existingLink)
            {
                stored.Id.Should().Be(linkId);
            }
        }
    }
}
